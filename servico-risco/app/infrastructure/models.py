"""Tabelas do banco (ORM). Ficam separadas das entidades do domínio.

Relacionamentos:
    importacoes_modelo 1 ── N scores_zona
    zonas              1 ── N scores_zona   (um score por faixa horária)
    zonas              1 ── N consultas_risco
"""
from __future__ import annotations

from datetime import datetime

from sqlalchemy import CheckConstraint, DateTime, Float, ForeignKey, Integer, String, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.infrastructure.database import Base


class ZonaModel(Base):
    __tablename__ = "zonas"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=False)
    nome: Mapped[str] = mapped_column(String(200), nullable=False)
    latitude: Mapped[float] = mapped_column(Float, nullable=False, index=True)
    longitude: Mapped[float] = mapped_column(Float, nullable=False, index=True)

    scores: Mapped[list["ScoreZonaModel"]] = relationship(
        back_populates="zona", cascade="all, delete-orphan", lazy="selectin")
    consultas: Mapped[list["ConsultaRiscoModel"]] = relationship(back_populates="zona", passive_deletes=True)


class ScoreZonaModel(Base):
    __tablename__ = "scores_zona"
    __table_args__ = (
        UniqueConstraint("zona_id", "faixa", name="uq_score_zona_faixa"),
        CheckConstraint("score >= 0 AND score <= 1", name="ck_score_intervalo"),
    )

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    zona_id: Mapped[int] = mapped_column(ForeignKey("zonas.id", ondelete="CASCADE"), nullable=False, index=True)
    faixa: Mapped[str] = mapped_column(String(12), nullable=False)
    score: Mapped[float] = mapped_column(Float, nullable=False)
    nivel: Mapped[str] = mapped_column(String(5), nullable=False)
    risco_relativo: Mapped[float | None] = mapped_column(Float)
    taxa_grave_observada: Mapped[float | None] = mapped_column(Float)
    n_sinistros: Mapped[int | None] = mapped_column(Integer)
    importacao_id: Mapped[int | None] = mapped_column(
        ForeignKey("importacoes_modelo.id", name="fk_scores_zona_importacao"), index=True)

    zona: Mapped[ZonaModel] = relationship(back_populates="scores")
    importacao: Mapped["ImportacaoModeloModel | None"] = relationship(back_populates="scores")


class ImportacaoModeloModel(Base):
    __tablename__ = "importacoes_modelo"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    arquivo: Mapped[str] = mapped_column(String(300), nullable=False)
    hash_sha256: Mapped[str] = mapped_column(String(64), nullable=False, index=True)
    total_zonas: Mapped[int] = mapped_column(Integer, nullable=False)
    total_linhas: Mapped[int] = mapped_column(Integer, nullable=False)
    criada_em: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)

    scores: Mapped[list[ScoreZonaModel]] = relationship(back_populates="importacao")


class ConsultaRiscoModel(Base):
    __tablename__ = "consultas_risco"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    latitude: Mapped[float] = mapped_column(Float, nullable=False)
    longitude: Mapped[float] = mapped_column(Float, nullable=False)
    hora: Mapped[int] = mapped_column(Integer, nullable=False)
    faixa: Mapped[str] = mapped_column(String(12), nullable=False)
    zona_id: Mapped[int | None] = mapped_column(ForeignKey("zonas.id", ondelete="SET NULL"), index=True)
    criada_em: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)

    zona: Mapped[ZonaModel | None] = relationship(back_populates="consultas")
