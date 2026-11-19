"""Testes de integração: API real + banco SQLite com as migrações aplicadas."""
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.config import Settings
from app.main import create_app
from tests.conftest import CSV_TESTE

PAULISTA = {"lat": -23.5612, "lng": -46.6558}
CSV_ML = Path(__file__).resolve().parents[3] / "ml" / "zonas_risco.csv"


def test_saude_mostra_banco_e_carga_inicial(client):
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json() == {"status": "ok", "banco": "ok", "zonas": 3, "importacaoAtiva": 1}


def test_consulta_por_ponto_segue_o_contrato_da_api(client):
    r = client.get("/api/zonas-risco", params={**PAULISTA, "hora": 2})
    assert r.status_code == 200
    corpo = r.json()
    assert [z["id"] for z in corpo] == [1, 2]                      # mais próxima primeiro; SP-270 fora do raio
    assert {"id", "nome", "nivel", "score"} <= corpo[0].keys()      # campos do contrato
    assert corpo[0] | {"distanciaKm": None} == {
        "id": 1, "nome": "Av. Paulista e entorno", "nivel": "ALTO", "score": 0.85, "faixaHoraria": "MADRUGADA",
        "latitude": -23.561, "longitude": -46.656, "distanciaKm": None, "riscoRelativo": 1.5}


def test_mesmo_lugar_muda_de_nivel_conforme_o_horario(client):
    madrugada = client.get("/api/zonas-risco", params={**PAULISTA, "hora": 3, "limite": 1}).json()[0]
    tarde = client.get("/api/zonas-risco", params={**PAULISTA, "hora": 15, "limite": 1}).json()[0]
    assert (madrugada["nivel"], tarde["nivel"]) == ("ALTO", "BAIXO")


def test_consulta_e_registrada_e_pode_ser_consultada_depois(client):
    client.get("/api/zonas-risco", params={**PAULISTA, "hora": 2})
    consultas = client.get("/api/consultas-risco").json()
    assert len(consultas) == 1
    assert consultas[0] | {"criadaEm": None} == {"id": 1, **{"latitude": -23.5612, "longitude": -46.6558},
                                                 "hora": 2, "faixaHoraria": "MADRUGADA", "zonaId": 1,
                                                 "criadaEm": None}


def test_sem_ponto_devolve_ranking_da_cidade(client):
    r = client.get("/api/zonas-risco", params={"hora": 1, "limite": 3})
    assert [(z["id"], z["score"]) for z in r.json()] == [(3, 1.0), (2, 0.95), (1, 0.85)]
    assert all(z["distanciaKm"] is None for z in r.json())


def test_sem_hora_usa_a_hora_atual(client):
    assert client.get("/api/zonas-risco").status_code == 200


def test_detalhe_da_zona_traz_todas_as_faixas(client):
    r = client.get("/api/zonas-risco/1")
    assert r.status_code == 200
    assert [(f["faixaHoraria"], f["horaInicio"], f["horaFim"], f["nivel"]) for f in r.json()["faixas"]] == [
        ("MADRUGADA", 0, 5, "ALTO"), ("TARDE", 12, 17, "BAIXO")]


@pytest.mark.parametrize("params, trecho", [
    ({"hora": 25}, "Hora inválida"),
    ({"hora": "abc"}, "Requisição inválida"),
    ({"lat": -23.56, "hora": 2}, "lat e lng juntos"),
    ({"lat": -95, "lng": -46.6, "hora": 2}, "Requisição inválida"),
    ({**PAULISTA, "hora": 2, "raioKm": 50}, "raio"),
    ({"hora": 2, "limite": 0}, "limite"),
])
def test_parametros_invalidos_devolvem_400(client, params, trecho):
    r = client.get("/api/zonas-risco", params=params)
    assert r.status_code == 400
    assert trecho in r.json()["erro"]


def test_zona_inexistente_devolve_404(client):
    r = client.get("/api/zonas-risco/9999")
    assert r.status_code == 404
    assert r.json()["erro"] == "Zona de risco 9999 não encontrada."


def test_upload_de_nova_versao_cria_importacao_e_troca_as_zonas(client):
    novo = CSV_TESTE.read_text(encoding="utf-8").replace("0.20,BAIXO", "0.60,MEDIO").encode()
    r = client.post("/api/importacoes", files={"arquivo": ("zonas_v2.csv", novo, "text/csv")})
    assert r.status_code == 201
    assert r.json() | {"criadaEm": None, "hashSha256": None} == {
        "id": 2, "arquivo": "zonas_v2.csv", "hashSha256": None, "totalZonas": 3, "totalLinhas": 5, "criadaEm": None}

    tarde = client.get("/api/zonas-risco/1").json()["faixas"][1]
    assert (tarde["score"], tarde["nivel"]) == (0.6, "MEDIO")
    assert [i["id"] for i in client.get("/api/importacoes").json()] == [2, 1]


def test_zona_que_sai_do_csv_some_mas_o_historico_de_consultas_fica(client):
    client.get("/api/zonas-risco", params={"lat": -23.59, "lng": -46.79, "hora": 2})  # consulta na zona 3
    sem_zona_3 = "\n".join(l for l in CSV_TESTE.read_text(encoding="utf-8").splitlines() if not l.startswith("3,"))
    assert client.post("/api/importacoes", files={"arquivo": ("v2.csv", sem_zona_3.encode(), "text/csv")}).status_code == 201
    assert client.get("/api/zonas-risco/3").status_code == 404
    assert client.get("/api/consultas-risco").json()[0]["zonaId"] is None


def test_reenviar_o_arquivo_ativo_devolve_409(client):
    r = client.post("/api/importacoes", files={"arquivo": ("de-novo.csv", CSV_TESTE.read_bytes(), "text/csv")})
    assert r.status_code == 409


def test_csv_invalido_devolve_400_e_nao_altera_as_zonas(client):
    ruim = CSV_TESTE.read_text(encoding="utf-8").replace("0.85,ALTO", "1.85,ALTO").encode()
    r = client.post("/api/importacoes", files={"arquivo": ("ruim.csv", ruim, "text/csv")})
    assert r.status_code == 400
    assert "Linha 2" in r.json()["erro"]
    assert client.get("/health").json()["zonas"] == 3
    assert len(client.get("/api/importacoes").json()) == 1


def test_swagger_documenta_rotas_exemplos_e_codigos(client):
    spec = client.get("/openapi.json").json()
    rota = spec["paths"]["/api/zonas-risco"]["get"]
    assert {"200", "400"} <= rota["responses"].keys()
    assert {"400", "404"} <= spec["paths"]["/api/zonas-risco/{zona_id}"]["get"]["responses"].keys()
    assert {"201", "400", "409"} <= spec["paths"]["/api/importacoes"]["post"]["responses"].keys()
    assert "example" in spec["components"]["schemas"]["ZonaRiscoResposta"]
    assert all("422" not in op["responses"] for rota in spec["paths"].values() for op in rota.values())
    assert client.get("/docs").status_code == 200


@pytest.mark.skipif(not CSV_ML.exists(), reason="ml/zonas_risco.csv ainda não foi gerado")
def test_csv_real_do_ml_e_aceito_inteiro(tmp_path):
    settings = Settings(database_url=f"sqlite:///{(tmp_path / 'real.db').as_posix()}", zonas_csv_path=CSV_ML)
    with TestClient(create_app(settings)) as c:
        assert c.get("/health").json()["zonas"] == 222
        imp = c.get("/api/importacoes").json()[0]
        assert (imp["totalZonas"], imp["totalLinhas"]) == (222, 794)
        r = c.get("/api/zonas-risco", params={"lat": -23.5905, "lng": -46.7889, "hora": 2, "limite": 1}).json()
        assert r[0]["nome"] == "SP-270 e entorno" and r[0]["nivel"] == "ALTO"
