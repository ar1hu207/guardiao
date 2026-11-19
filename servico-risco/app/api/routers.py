"""Rotas REST. Só traduzem HTTP <-> serviço; nenhuma regra de negócio mora aqui."""
from __future__ import annotations

from datetime import datetime
from zoneinfo import ZoneInfo

from fastapi import APIRouter, Depends, File, Query, Request, UploadFile, status

from app.api.dependencies import get_importacao_service, get_uow, get_zona_service
from app.api.schemas import (ConsultaResposta, ErroResposta, ImportacaoResposta, SaudeResposta,
                             ZonaDetalheResposta, ZonaRiscoResposta)
from app.domain.entities import Coordenada
from app.domain.exceptions import DominioInvalido
from app.services.importacao_service import ImportacaoService
from app.services.zona_risco_service import ZonaRiscoService

ERRO_400 = {400: {"model": ErroResposta, "description": "Parâmetro inválido (ex.: hora fora de 0–23)."}}
ERRO_404 = {404: {"model": ErroResposta, "description": "Zona não encontrada."}}

zonas_router = APIRouter(prefix="/api/zonas-risco", tags=["Zonas de risco"])
consultas_router = APIRouter(prefix="/api/consultas-risco", tags=["Auditoria"])
importacoes_router = APIRouter(prefix="/api/importacoes", tags=["Importações do modelo de ML"])
saude_router = APIRouter(tags=["Saúde"])


def _hora_atual(request: Request) -> int:
    return datetime.now(ZoneInfo(request.app.state.settings.fuso_horario)).hour


@zonas_router.get(
    "",
    response_model=list[ZonaRiscoResposta],
    summary="Risco da região naquele horário",
    description=(
        "Rota usada pelo app e pela API do Guardião.\n\n"
        "- **Com `lat` e `lng`**: devolve as zonas num raio de `raioKm` ao redor do ponto, da mais próxima para a "
        "mais distante, com o score da faixa horária de `hora`. A primeira é a zona onde o motoboy está. "
        "A consulta é registrada na auditoria.\n"
        "- **Sem `lat`/`lng`**: devolve o ranking das zonas mais arriscadas da cidade naquela faixa horária.\n\n"
        "Sem `hora`, usa a hora atual de São Paulo. Faixas: madrugada 0–5h, manhã 6–11h, tarde 12–17h, noite 18–23h."
    ),
    responses={**ERRO_400, 200: {"description": "Lista de zonas (pode ser vazia se não houver zona no raio)."}},
)
def listar_zonas(
    request: Request,
    lat: float | None = Query(None, ge=-90, le=90, description="Latitude do motoboy.", examples=[-23.5905]),
    lng: float | None = Query(None, ge=-180, le=180, description="Longitude do motoboy.", examples=[-46.7889]),
    hora: int | None = Query(None, description="Hora do dia (0 a 23).", examples=[2]),
    raio_km: float | None = Query(None, alias="raioKm", description="Raio de busca em km (padrão 2, máximo 20)."),
    limite: int = Query(5, description="Máximo de zonas devolvidas (1 a 50)."),
    service: ZonaRiscoService = Depends(get_zona_service),
) -> list[ZonaRiscoResposta]:
    hora = _hora_atual(request) if hora is None else hora
    if (lat is None) != (lng is None):
        raise DominioInvalido("Informe lat e lng juntos, ou nenhum dos dois para ver o ranking.")
    if lat is None:
        resultado = service.ranking(hora, limite)
    else:
        raio = request.app.state.settings.raio_padrao_km if raio_km is None else raio_km
        resultado = service.consultar_proximas(Coordenada(lat, lng), hora, raio, limite)
    return [ZonaRiscoResposta.de(r) for r in resultado]


@zonas_router.get(
    "/{zona_id}",
    response_model=ZonaDetalheResposta,
    summary="Detalhe de uma zona nas quatro faixas horárias",
    responses={**ERRO_400, **ERRO_404},
)
def obter_zona(zona_id: int, service: ZonaRiscoService = Depends(get_zona_service)) -> ZonaDetalheResposta:
    return ZonaDetalheResposta.de(service.obter(zona_id))


@consultas_router.get(
    "",
    response_model=list[ConsultaResposta],
    summary="Últimas consultas de risco feitas pelo app",
    description="Auditoria das consultas (ponto, hora e zona encontrada). Não guarda nenhum dado do motoboy.",
    responses=ERRO_400,
)
def listar_consultas(limite: int = Query(20, description="1 a 50."),
                     service: ZonaRiscoService = Depends(get_zona_service)) -> list[ConsultaResposta]:
    return [ConsultaResposta.de(c) for c in service.consultas_recentes(limite)]


@importacoes_router.post(
    "",
    response_model=ImportacaoResposta,
    status_code=status.HTTP_201_CREATED,
    summary="Carregar uma nova versão das zonas de risco",
    description=(
        "Recebe o `zonas_risco.csv` exportado pelo notebook de ML e troca todas as zonas numa transação. "
        "Sem arquivo, recarrega o CSV configurado em `ZONAS_CSV_PATH`. Se qualquer linha for inválida, "
        "nada muda e a resposta é 400. Reenviar o arquivo da importação ativa devolve 409."
    ),
    responses={**ERRO_400, 409: {"model": ErroResposta, "description": "Arquivo igual ao da importação ativa."}},
)
async def importar(request: Request, arquivo: UploadFile | None = File(None, description="zonas_risco.csv"),
                   service: ImportacaoService = Depends(get_importacao_service)) -> ImportacaoResposta:
    if arquivo is not None:
        conteudo, nome = await arquivo.read(), arquivo.filename or "upload.csv"
    else:
        caminho = request.app.state.settings.zonas_csv_path
        if not caminho.exists():
            raise DominioInvalido(f"Arquivo padrão não encontrado: {caminho}")
        conteudo, nome = caminho.read_bytes(), caminho.name
    return ImportacaoResposta.de(service.importar(conteudo, nome))


@importacoes_router.get("", response_model=list[ImportacaoResposta], summary="Histórico de importações")
def listar_importacoes(service: ImportacaoService = Depends(get_importacao_service)) -> list[ImportacaoResposta]:
    return [ImportacaoResposta.de(i) for i in service.listar()]


@saude_router.get("/health", response_model=SaudeResposta, summary="Saúde do serviço e do banco")
def saude(uow=Depends(get_uow)) -> SaudeResposta:
    uow.verificar_conexao()
    ativa = uow.importacoes.ultima()
    return SaudeResposta(status="ok", banco="ok", zonas=uow.zonas.contar(), importacao_ativa=ativa.id if ativa else None)
