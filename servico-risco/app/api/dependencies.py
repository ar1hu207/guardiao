"""Injeção de dependência: cada requisição ganha sua sessão, unidade de trabalho e serviços."""
from __future__ import annotations

from collections.abc import Iterator

from fastapi import Depends, Request

from app.infrastructure.repositories import SqlUnidadeDeTrabalho
from app.services.importacao_service import ImportacaoService
from app.services.zona_risco_service import ZonaRiscoService


def get_uow(request: Request) -> Iterator[SqlUnidadeDeTrabalho]:
    session = request.app.state.fabrica_sessao()
    try:
        yield SqlUnidadeDeTrabalho(session)
    finally:
        session.close()


def get_zona_service(uow: SqlUnidadeDeTrabalho = Depends(get_uow)) -> ZonaRiscoService:
    return ZonaRiscoService(uow)


def get_importacao_service(uow: SqlUnidadeDeTrabalho = Depends(get_uow)) -> ImportacaoService:
    return ImportacaoService(uow)
