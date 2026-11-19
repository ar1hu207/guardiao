"""Contratos (interfaces) de persistência. O domínio depende deles; a infraestrutura os implementa."""
from __future__ import annotations

from typing import Protocol

from app.domain.entities import ConsultaRisco, ImportacaoModelo, ZonaDeRisco
from app.domain.enums import FaixaHoraria


class ZonaRepository(Protocol):
    def obter(self, zona_id: int) -> ZonaDeRisco | None: ...

    def listar_na_caixa(self, lat_min: float, lat_max: float, lng_min: float, lng_max: float,
                        faixa: FaixaHoraria) -> list[ZonaDeRisco]: ...

    def ranking(self, faixa: FaixaHoraria, limite: int) -> list[ZonaDeRisco]: ...

    def contar(self) -> int: ...

    def substituir_todas(self, zonas: list[ZonaDeRisco], importacao_id: int) -> None: ...


class ImportacaoRepository(Protocol):
    def adicionar(self, importacao: ImportacaoModelo) -> ImportacaoModelo: ...

    def ultima(self) -> ImportacaoModelo | None: ...

    def listar(self) -> list[ImportacaoModelo]: ...


class ConsultaRepository(Protocol):
    def adicionar(self, consulta: ConsultaRisco) -> ConsultaRisco: ...

    def listar_recentes(self, limite: int) -> list[ConsultaRisco]: ...


class UnidadeDeTrabalho(Protocol):
    """Agrupa os repositórios numa transação: tudo é gravado junto ou nada é gravado."""

    zonas: ZonaRepository
    importacoes: ImportacaoRepository
    consultas: ConsultaRepository

    def commit(self) -> None: ...

    def rollback(self) -> None: ...
