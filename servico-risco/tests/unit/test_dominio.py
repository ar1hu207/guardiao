"""Testes de unidade das regras do domínio (sem banco, sem HTTP)."""
import math

import pytest

from app.domain.entities import Coordenada, ImportacaoModelo, ScoreFaixa, ZonaDeRisco
from app.domain.enums import FaixaHoraria, NivelRisco
from app.domain.exceptions import DominioInvalido
from app.infrastructure.csv_zonas import ler_zonas
from tests.conftest import CSV_TESTE

SE = Coordenada(-23.5503, -46.6339)


# ---------- FaixaHoraria ----------

@pytest.mark.parametrize("hora, faixa", [
    (0, FaixaHoraria.MADRUGADA), (5, FaixaHoraria.MADRUGADA),
    (6, FaixaHoraria.MANHA), (11, FaixaHoraria.MANHA),
    (12, FaixaHoraria.TARDE), (17, FaixaHoraria.TARDE),
    (18, FaixaHoraria.NOITE), (23, FaixaHoraria.NOITE),
])
def test_hora_vira_faixa_do_infosiga(hora, faixa):
    assert FaixaHoraria.de_hora(hora) is faixa


@pytest.mark.parametrize("hora", [-1, 24, 2.5, True, "3"])
def test_hora_invalida_e_recusada(hora):
    with pytest.raises(DominioInvalido):
        FaixaHoraria.de_hora(hora)


def test_faixa_conhece_seu_intervalo():
    assert (FaixaHoraria.NOITE.hora_inicio, FaixaHoraria.NOITE.hora_fim) == (18, 23)


# ---------- NivelRisco ----------

@pytest.mark.parametrize("score, nivel", [
    (0.0, NivelRisco.BAIXO), (0.39, NivelRisco.BAIXO), (0.40, NivelRisco.MEDIO),
    (0.79, NivelRisco.MEDIO), (0.80, NivelRisco.ALTO), (1.0, NivelRisco.ALTO),
])
def test_nivel_e_derivado_do_score_com_os_cortes_do_ml(score, nivel):
    assert NivelRisco.de_score(score) is nivel


# ---------- ScoreFaixa ----------

@pytest.mark.parametrize("score", [-0.01, 1.01, math.nan])
def test_score_fora_de_0_a_1_e_recusado(score):
    with pytest.raises(DominioInvalido):
        ScoreFaixa(FaixaHoraria.TARDE, score)


def test_score_nao_aceita_contagem_negativa():
    with pytest.raises(DominioInvalido):
        ScoreFaixa(FaixaHoraria.TARDE, 0.5, n_sinistros=-1)


def test_nivel_do_score_nao_pode_ser_escolhido_a_mao():
    assert ScoreFaixa(FaixaHoraria.MADRUGADA, 0.81).nivel is NivelRisco.ALTO


# ---------- ZonaDeRisco ----------

def _zona(**kw) -> ZonaDeRisco:
    base = dict(id=1, nome="Av. Paulista e entorno", centro=Coordenada(-23.561, -46.656))
    return ZonaDeRisco(**{**base, **kw})


def test_zona_tem_um_score_por_faixa():
    zona = _zona()
    zona.adicionar_score(ScoreFaixa(FaixaHoraria.MADRUGADA, 0.9))
    with pytest.raises(DominioInvalido):
        zona.adicionar_score(ScoreFaixa(FaixaHoraria.MADRUGADA, 0.1))


def test_zona_responde_score_pela_hora():
    zona = _zona()
    zona.adicionar_score(ScoreFaixa(FaixaHoraria.MADRUGADA, 0.9))
    assert zona.score_na_hora(3).score == 0.9
    assert zona.score_na_hora(14) is None


def test_scores_saem_na_ordem_do_dia():
    zona = _zona()
    for faixa in (FaixaHoraria.NOITE, FaixaHoraria.MADRUGADA, FaixaHoraria.TARDE):
        zona.adicionar_score(ScoreFaixa(faixa, 0.5))
    assert [s.faixa for s in zona.scores] == [FaixaHoraria.MADRUGADA, FaixaHoraria.TARDE, FaixaHoraria.NOITE]


@pytest.mark.parametrize("kw", [{"id": 0}, {"nome": "   "}])
def test_zona_invalida_e_recusada(kw):
    with pytest.raises(DominioInvalido):
        _zona(**kw)


# ---------- Coordenada ----------

@pytest.mark.parametrize("lat, lng", [(-91, 0), (91, 0), (0, -181), (0, 181)])
def test_coordenada_fora_do_globo_e_recusada(lat, lng):
    with pytest.raises(DominioInvalido):
        Coordenada(lat, lng)


def test_distancia_de_um_grau_de_latitude_e_cerca_de_111_km():
    assert Coordenada(0, 0).distancia_km(Coordenada(1, 0)) == pytest.approx(111.19, abs=0.05)


def test_distancia_e_simetrica_e_zero_no_mesmo_ponto():
    paulista = Coordenada(-23.5614, -46.6559)
    assert SE.distancia_km(paulista) == pytest.approx(paulista.distancia_km(SE))
    assert SE.distancia_km(SE) == 0


def test_caixa_contem_os_pontos_do_raio():
    lat_min, lat_max, lng_min, lng_max = SE.caixa_ao_redor(2.0)
    for ponto in (Coordenada(lat_max, SE.longitude), Coordenada(SE.latitude, lng_min)):
        assert SE.distancia_km(ponto) == pytest.approx(2.0, rel=1e-3)


# ---------- ImportacaoModelo ----------

def test_importacao_exige_hash_sha256_e_zonas():
    from datetime import datetime, timezone
    agora = datetime.now(timezone.utc)
    with pytest.raises(DominioInvalido):
        ImportacaoModelo(arquivo="x.csv", hash_sha256="abc", total_zonas=1, total_linhas=1, criada_em=agora)
    with pytest.raises(DominioInvalido):
        ImportacaoModelo(arquivo="x.csv", hash_sha256="a" * 64, total_zonas=0, total_linhas=0, criada_em=agora)


# ---------- Leitura do CSV do ML ----------

def test_csv_do_ml_vira_zonas_do_dominio():
    zonas, linhas = ler_zonas(CSV_TESTE.read_bytes())
    assert linhas == 5
    assert {z.id for z in zonas} == {1, 2, 3}
    paulista = next(z for z in zonas if z.id == 1)
    assert [s.faixa for s in paulista.scores] == [FaixaHoraria.MADRUGADA, FaixaHoraria.TARDE]


def test_csv_com_nivel_incoerente_com_o_score_e_recusado():
    conteudo = CSV_TESTE.read_text(encoding="utf-8").replace("0.85,ALTO", "0.85,BAIXO").encode()
    with pytest.raises(DominioInvalido, match="Linha 2"):
        ler_zonas(conteudo)


def test_csv_sem_coluna_obrigatoria_e_recusado():
    with pytest.raises(DominioInvalido, match="score"):
        ler_zonas(b"zona_id,nome,latitude,longitude,faixa_horaria,nivel\n1,X,-23.5,-46.6,TARDE,BAIXO\n")


def test_csv_fora_de_utf8_e_recusado():
    with pytest.raises(DominioInvalido, match="UTF-8"):
        ler_zonas("zona_id,nome\n1,São Paulo\n".encode("latin-1"))
