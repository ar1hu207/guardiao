"""Serviço de Risco do Guardião: ponto de entrada da aplicação FastAPI."""
from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.openapi.utils import get_openapi
from fastapi.responses import JSONResponse

from app.api.routers import consultas_router, importacoes_router, saude_router, zonas_router
from app.config import Settings, get_settings
from app.domain.exceptions import DominioInvalido, ImportacaoDuplicada, ZonaNaoEncontrada
from app.infrastructure.database import criar_engine, criar_fabrica_sessao
from app.infrastructure.migracoes import aplicar_migracoes
from app.infrastructure.repositories import SqlUnidadeDeTrabalho
from app.services.importacao_service import ImportacaoService

log = logging.getLogger("guardiao.risco")

DESCRICAO = """
Serviço de **zonas de risco** do Guardião (Global Solution FIAP 2026.2 — CitizenTech).

Ele é dono de uma única responsabilidade: dizer **quão arriscada é uma região de São Paulo num horário** para
o motoboy de aplicativo. O score vem do modelo de Machine Learning (XGBoost treinado com 124.935 sinistros reais
com moto do **Infosiga SP**, 2019–2026), exportado em `ml/zonas_risco.csv`.

A API principal do Guardião (C#) e o app consultam este serviço por REST:
`GET /api/zonas-risco?lat=-23.59&lng=-46.79&hora=2`.
"""


def _carga_inicial(app: FastAPI) -> None:
    settings: Settings = app.state.settings
    if not settings.importar_csv_se_vazio:
        return
    session = app.state.fabrica_sessao()
    try:
        uow = SqlUnidadeDeTrabalho(session)
        if uow.zonas.contar() == 0 and settings.zonas_csv_path.exists():
            imp = ImportacaoService(uow).importar(settings.zonas_csv_path.read_bytes(), settings.zonas_csv_path.name)
            log.info("Carga inicial: %s zonas importadas de %s", imp.total_zonas, settings.zonas_csv_path)
    finally:
        session.close()


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or get_settings()

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        if settings.aplicar_migracoes_na_inicializacao:
            aplicar_migracoes(settings.database_url)
        _carga_inicial(app)
        yield
        app.state.engine.dispose()

    app = FastAPI(
        title="Guardião — Serviço de Risco",
        version="1.0.0",
        description=DESCRICAO,
        lifespan=lifespan,
        contact={"name": "Equipe Guardião — FIAP"},
        openapi_tags=[
            {"name": "Zonas de risco", "description": "Consulta do risco por região e horário."},
            {"name": "Importações do modelo de ML", "description": "Carga de novas versões do `zonas_risco.csv`."},
            {"name": "Auditoria", "description": "Histórico das consultas feitas pelo app."},
            {"name": "Saúde", "description": "Verificação para monitoramento e deploy."},
        ],
    )
    app.state.settings = settings
    app.state.engine = criar_engine(settings.database_url)
    app.state.fabrica_sessao = criar_fabrica_sessao(app.state.engine)

    for router in (zonas_router, consultas_router, importacoes_router, saude_router):
        app.include_router(router)

    def openapi_sem_422():
        """Erros de validação viram 400 (handler abaixo); a documentação precisa dizer o mesmo."""
        if app.openapi_schema:
            return app.openapi_schema
        schema = get_openapi(title=app.title, version=app.version, description=app.description,
                             routes=app.routes, tags=app.openapi_tags, contact=app.contact)
        for rota in schema["paths"].values():
            for operacao in rota.values():
                operacao.get("responses", {}).pop("422", None)
        for nome in ("HTTPValidationError", "ValidationError"):
            schema.get("components", {}).get("schemas", {}).pop(nome, None)
        app.openapi_schema = schema
        return schema

    app.openapi = openapi_sem_422

    @app.exception_handler(RequestValidationError)
    async def _validacao(_: Request, exc: RequestValidationError):
        detalhes = [f"{'.'.join(str(p) for p in e['loc'])}: {e['msg']}" for e in exc.errors()]
        return JSONResponse(status_code=400, content={"erro": "Requisição inválida.", "detalhes": detalhes})

    @app.exception_handler(DominioInvalido)
    async def _dominio(_: Request, exc: DominioInvalido):
        return JSONResponse(status_code=400, content={"erro": str(exc), "detalhes": []})

    @app.exception_handler(ZonaNaoEncontrada)
    async def _nao_encontrada(_: Request, exc: ZonaNaoEncontrada):
        return JSONResponse(status_code=404, content={"erro": str(exc), "detalhes": []})

    @app.exception_handler(ImportacaoDuplicada)
    async def _duplicada(_: Request, exc: ImportacaoDuplicada):
        return JSONResponse(status_code=409, content={"erro": str(exc), "detalhes": []})

    return app


app = create_app()
