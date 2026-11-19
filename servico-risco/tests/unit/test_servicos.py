"""Testes de unidade dos serviços, com repositórios falsos em memória (sem banco)."""
from datetime import datetime, timezone

import pytest

from app.domain.entities import ConsultaRisco, Coordenada, ImportacaoModelo, ScoreFaixa, ZonaDeRisco
from app.domain.enums import FaixaHoraria
from app.domain.exceptions import DominioInvalido, ImportacaoDuplicada, ZonaNaoEncontrada
from app.services.importacao_service import ImportacaoService
from app.services.zona_risco_service import ZonaRiscoService
from tests.conftest import CSV_TESTE

AGORA = datetime(2026, 11, 15, 2, 41, tzinfo=timezone.utc)


class FakeZonas:
    def __init__(self, zonas):
        self.zonas = {z.id: z for z in zonas}

    def obter(self, zona_id):
        return self.zonas.get(zona_id)

    def listar_na_caixa(self, lat_min, lat_max, lng_min, lng_max, faixa):
        return [z for z in self.zonas.values() if lat_min <= z.centro.latitude <= lat_max
                and lng_min <= z.centro.longitude <= lng_max and z.score_na_faixa(faixa)]

    def ranking(self, faixa, limite):
        com = [z for z in self.zonas.values() if z.score_na_faixa(faixa)]
        return sorted(com, key=lambda z: -z.score_na_faixa(faixa).score)[:limite]

    def contar(self):
        return len(self.zonas)

    def substituir_todas(self, zonas, importacao_id):
        self.zonas = {z.id: z for z in zonas}


class FakeLista:
    def __init__(self):
        self.itens = []

    def adicionar(self, item):
        item.id = len(self.itens) + 1
        self.itens.append(item)
        return item

    def ultima(self):
        return self.itens[-1] if self.itens else None

    def listar(self):
        return list(reversed(self.itens))

    def listar_recentes(self, limite):
        return list(reversed(self.itens))[:limite]


class FakeUow:
    def __init__(self, zonas=()):
        self.zonas = FakeZonas(zonas)
        self.importacoes = FakeLista()
        self.consultas = FakeLista()
        self.commits = 0
        self.rollbacks = 0

    def commit(self):
        self.commits += 1

    def rollback(self):
        self.rollbacks += 1


def zona(id_, lat, lng, **scores):
    z = ZonaDeRisco(id=id_, nome=f"Zona {id_}", centro=Coordenada(lat, lng))
    for faixa, score in scores.items():
        z.adicionar_score(ScoreFaixa(FaixaHoraria[faixa], score))
    return z


@pytest.fixture
def uow():
    return FakeUow([
        zona(1, -23.561, -46.656, MADRUGADA=0.85, TARDE=0.20),   # Paulista
        zona(2, -23.555, -46.662, MADRUGADA=0.95),               # Consolação (~0,8 km)
        zona(3, -23.590, -46.790, MADRUGADA=1.00),               # SP-270 (~14 km)
    ])


def test_consulta_devolve_zonas_do_raio_da_mais_proxima_para_a_mais_distante(uow):
    service = ZonaRiscoService(uow, relogio=lambda: AGORA)
    resultado = service.consultar_proximas(Coordenada(-23.5612, -46.6558), hora=2, raio_km=2, limite=5)
    assert [r.zona.id for r in resultado] == [1, 2]          # a zona 3 está fora do raio
    assert resultado[0].distancia_km < resultado[1].distancia_km
    assert all(r.score.faixa is FaixaHoraria.MADRUGADA for r in resultado)


def test_consulta_usa_o_score_da_faixa_da_hora(uow):
    resultado = ZonaRiscoService(uow).consultar_proximas(Coordenada(-23.561, -46.656), hora=14, raio_km=2, limite=5)
    assert [(r.zona.id, r.score.score) for r in resultado] == [(1, 0.20)]  # só a zona 1 tem score à tarde


def test_toda_consulta_fica_registrada_com_a_zona_mais_proxima(uow):
    ZonaRiscoService(uow, relogio=lambda: AGORA).consultar_proximas(Coordenada(-23.5612, -46.6558), 2, 2, 5)
    consulta: ConsultaRisco = uow.consultas.itens[0]
    assert (consulta.zona_id, consulta.faixa, consulta.criada_em) == (1, FaixaHoraria.MADRUGADA, AGORA)
    assert uow.commits == 1


def test_consulta_longe_de_tudo_registra_sem_zona(uow):
    resultado = ZonaRiscoService(uow).consultar_proximas(Coordenada(-23.9, -46.4), 2, 1, 5)
    assert resultado == []
    assert uow.consultas.itens[0].zona_id is None


@pytest.mark.parametrize("raio, limite", [(0, 5), (21, 5), (2, 0), (2, 51)])
def test_raio_e_limite_tem_teto(uow, raio, limite):
    with pytest.raises(DominioInvalido):
        ZonaRiscoService(uow).consultar_proximas(Coordenada(-23.56, -46.65), 2, raio, limite)


def test_ranking_ordena_pelo_score_da_faixa(uow):
    assert [r.zona.id for r in ZonaRiscoService(uow).ranking(hora=3, limite=2)] == [3, 2]


def test_zona_inexistente_gera_erro_de_nao_encontrada(uow):
    with pytest.raises(ZonaNaoEncontrada):
        ZonaRiscoService(uow).obter(999)


def test_importacao_troca_as_zonas_e_guarda_a_versao():
    uow = FakeUow()
    imp = ImportacaoService(uow, relogio=lambda: AGORA).importar(CSV_TESTE.read_bytes(), "zonas_teste.csv")
    assert (imp.id, imp.total_zonas, imp.total_linhas, imp.criada_em) == (1, 3, 5, AGORA)
    assert uow.zonas.contar() == 3 and uow.commits == 1


def test_reimportar_o_mesmo_arquivo_e_recusado():
    uow = FakeUow()
    service = ImportacaoService(uow)
    service.importar(CSV_TESTE.read_bytes(), "a.csv")
    with pytest.raises(ImportacaoDuplicada):
        service.importar(CSV_TESTE.read_bytes(), "b.csv")
    assert len(uow.importacoes.itens) == 1


def test_csv_invalido_nao_altera_nada(uow):
    with pytest.raises(DominioInvalido):
        ImportacaoService(uow).importar(b"zona_id,nome\n1,X\n", "ruim.csv")
    assert uow.zonas.contar() == 3 and uow.importacoes.itens == [] and uow.commits == 0


def test_importacao_desfaz_tudo_se_a_gravacao_falhar(uow):
    def explode(*_, **__):
        raise RuntimeError("disco cheio")
    uow.zonas.substituir_todas = explode
    with pytest.raises(RuntimeError):
        ImportacaoService(uow).importar(CSV_TESTE.read_bytes(), "zonas.csv")
    assert uow.rollbacks == 1 and uow.commits == 0


def test_importacao_modelo_guarda_hash_do_arquivo():
    uow = FakeUow()
    imp: ImportacaoModelo = ImportacaoService(uow).importar(CSV_TESTE.read_bytes(), "zonas.csv")
    import hashlib
    assert imp.hash_sha256 == hashlib.sha256(CSV_TESTE.read_bytes()).hexdigest()
