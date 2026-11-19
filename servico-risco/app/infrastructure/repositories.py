"""Implementação dos repositórios com SQLAlchemy e tradução ORM <-> domínio."""
from __future__ import annotations

from datetime import datetime, timezone

from sqlalchemy import delete, func, select, text
from sqlalchemy.orm import Session

from app.domain.entities import ConsultaRisco, Coordenada, ImportacaoModelo, ScoreFaixa, ZonaDeRisco
from app.domain.enums import FaixaHoraria
from app.infrastructure.models import ConsultaRiscoModel, ImportacaoModeloModel, ScoreZonaModel, ZonaModel


# ---------- tradução ORM -> domínio ----------

def _utc(dt: datetime) -> datetime:
    """O SQLite não guarda fuso: as datas são gravadas em UTC e voltam marcadas como UTC."""
    return dt.replace(tzinfo=timezone.utc) if dt.tzinfo is None else dt


def _zona_para_dominio(m: ZonaModel) -> ZonaDeRisco:
    zona = ZonaDeRisco(id=m.id, nome=m.nome, centro=Coordenada(m.latitude, m.longitude))
    for s in m.scores:
        zona.adicionar_score(ScoreFaixa(
            faixa=FaixaHoraria(s.faixa), score=s.score, risco_relativo=s.risco_relativo,
            taxa_grave_observada=s.taxa_grave_observada, n_sinistros=s.n_sinistros))
    return zona


def _importacao_para_dominio(m: ImportacaoModeloModel) -> ImportacaoModelo:
    return ImportacaoModelo(id=m.id, arquivo=m.arquivo, hash_sha256=m.hash_sha256, total_zonas=m.total_zonas,
                            total_linhas=m.total_linhas, criada_em=_utc(m.criada_em))


def _consulta_para_dominio(m: ConsultaRiscoModel) -> ConsultaRisco:
    return ConsultaRisco(id=m.id, ponto=Coordenada(m.latitude, m.longitude), hora=m.hora,
                         faixa=FaixaHoraria(m.faixa), zona_id=m.zona_id, criada_em=_utc(m.criada_em))


# ---------- repositórios ----------

class SqlZonaRepository:
    def __init__(self, session: Session):
        self._s = session

    def obter(self, zona_id: int) -> ZonaDeRisco | None:
        m = self._s.get(ZonaModel, zona_id)
        return _zona_para_dominio(m) if m else None

    def listar_na_caixa(self, lat_min: float, lat_max: float, lng_min: float, lng_max: float,
                        faixa: FaixaHoraria) -> list[ZonaDeRisco]:
        stmt = (select(ZonaModel)
                .join(ScoreZonaModel)
                .where(ZonaModel.latitude.between(lat_min, lat_max),
                       ZonaModel.longitude.between(lng_min, lng_max),
                       ScoreZonaModel.faixa == faixa.value)
                .distinct())
        return [_zona_para_dominio(m) for m in self._s.scalars(stmt)]

    def ranking(self, faixa: FaixaHoraria, limite: int) -> list[ZonaDeRisco]:
        stmt = (select(ZonaModel)
                .join(ScoreZonaModel)
                .where(ScoreZonaModel.faixa == faixa.value)
                .order_by(ScoreZonaModel.score.desc(), ScoreZonaModel.risco_relativo.desc(), ZonaModel.id)
                .limit(limite))
        return [_zona_para_dominio(m) for m in self._s.scalars(stmt)]

    def contar(self) -> int:
        return self._s.scalar(select(func.count()).select_from(ZonaModel)) or 0

    def substituir_todas(self, zonas: list[ZonaDeRisco], importacao_id: int) -> None:
        """Troca o conjunto de zonas pelo da nova importação.

        Zonas que continuam existindo são atualizadas (o histórico de consultas fica ligado a elas);
        zonas que sumiram são apagadas (as consultas antigas ficam com zona_id nulo).
        """
        self._s.execute(delete(ScoreZonaModel))
        self._s.expire_all()
        existentes = {m.id: m for m in self._s.scalars(select(ZonaModel))}
        novas_ids = {z.id for z in zonas}
        for zona_id, m in existentes.items():
            if zona_id not in novas_ids:
                self._s.delete(m)
        for z in zonas:
            m = existentes.get(z.id) or ZonaModel(id=z.id)
            m.nome = z.nome
            m.latitude = z.centro.latitude
            m.longitude = z.centro.longitude
            m.scores = [ScoreZonaModel(
                faixa=s.faixa.value, score=s.score, nivel=s.nivel.value, risco_relativo=s.risco_relativo,
                taxa_grave_observada=s.taxa_grave_observada, n_sinistros=s.n_sinistros,
                importacao_id=importacao_id) for s in z.scores]
            self._s.add(m)
        self._s.flush()


class SqlImportacaoRepository:
    def __init__(self, session: Session):
        self._s = session

    def adicionar(self, importacao: ImportacaoModelo) -> ImportacaoModelo:
        m = ImportacaoModeloModel(arquivo=importacao.arquivo, hash_sha256=importacao.hash_sha256,
                                  total_zonas=importacao.total_zonas, total_linhas=importacao.total_linhas,
                                  criada_em=importacao.criada_em)
        self._s.add(m)
        self._s.flush()
        importacao.id = m.id
        return importacao

    def ultima(self) -> ImportacaoModelo | None:
        m = self._s.scalars(select(ImportacaoModeloModel).order_by(ImportacaoModeloModel.id.desc()).limit(1)).first()
        return _importacao_para_dominio(m) if m else None

    def listar(self) -> list[ImportacaoModelo]:
        stmt = select(ImportacaoModeloModel).order_by(ImportacaoModeloModel.id.desc())
        return [_importacao_para_dominio(m) for m in self._s.scalars(stmt)]


class SqlConsultaRepository:
    def __init__(self, session: Session):
        self._s = session

    def adicionar(self, consulta: ConsultaRisco) -> ConsultaRisco:
        m = ConsultaRiscoModel(latitude=consulta.ponto.latitude, longitude=consulta.ponto.longitude,
                               hora=consulta.hora, faixa=consulta.faixa.value, zona_id=consulta.zona_id,
                               criada_em=consulta.criada_em)
        self._s.add(m)
        self._s.flush()
        consulta.id = m.id
        return consulta

    def listar_recentes(self, limite: int) -> list[ConsultaRisco]:
        stmt = select(ConsultaRiscoModel).order_by(ConsultaRiscoModel.id.desc()).limit(limite)
        return [_consulta_para_dominio(m) for m in self._s.scalars(stmt)]


class SqlUnidadeDeTrabalho:
    """Uma sessão = uma transação, compartilhada pelos três repositórios."""

    def __init__(self, session: Session):
        self._s = session
        self.zonas = SqlZonaRepository(session)
        self.importacoes = SqlImportacaoRepository(session)
        self.consultas = SqlConsultaRepository(session)

    def verificar_conexao(self) -> None:
        self._s.execute(text("SELECT 1"))

    def commit(self) -> None:
        self._s.commit()

    def rollback(self) -> None:
        self._s.rollback()
