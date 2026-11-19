"""DTOs de saída. A entidade do domínio nunca é exposta direto na rota."""
from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

from app.domain.entities import ConsultaRisco, ImportacaoModelo, ScoreFaixa, ZonaConsultada, ZonaDeRisco
from app.domain.enums import FaixaHoraria, NivelRisco


class _Dto(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class ZonaRiscoResposta(_Dto):
    """Item da lista de `GET /api/zonas-risco` (formato do contrato da API, com campos extras)."""

    id: int = Field(description="Id da zona (célula de ~2 km).")
    nome: str = Field(description="Logradouro com mais sinistros dentro da zona.")
    nivel: NivelRisco = Field(description="BAIXO, MEDIO ou ALTO, derivado do score.")
    score: float = Field(ge=0, le=1, description="0 a 1: posição da zona no ranking de risco do modelo de ML.")
    faixa_horaria: FaixaHoraria
    latitude: float
    longitude: float
    distancia_km: float | None = Field(None, description="Distância do ponto consultado ao centro da zona.")
    risco_relativo: float | None = Field(None, description="Probabilidade do modelo ÷ média da cidade.")

    model_config = ConfigDict(json_schema_extra={"example": {
        "id": 78, "nome": "SP-270 e entorno", "nivel": "ALTO", "score": 1.0, "faixaHoraria": "MADRUGADA",
        "latitude": -23.59, "longitude": -46.79, "distanciaKm": 0.42, "riscoRelativo": 1.66}})

    @classmethod
    def de(cls, r: ZonaConsultada) -> "ZonaRiscoResposta":
        return cls(id=r.zona.id, nome=r.zona.nome, nivel=r.score.nivel, score=r.score.score,
                   faixa_horaria=r.score.faixa, latitude=r.zona.centro.latitude, longitude=r.zona.centro.longitude,
                   distancia_km=r.distancia_km, risco_relativo=r.score.risco_relativo)


class ScoreFaixaResposta(_Dto):
    faixa_horaria: FaixaHoraria
    hora_inicio: int
    hora_fim: int
    score: float
    nivel: NivelRisco
    risco_relativo: float | None
    taxa_grave_observada: float | None = Field(description="% real de sinistros graves (Infosiga) na zona e faixa.")
    n_sinistros: int | None

    @classmethod
    def de(cls, s: ScoreFaixa) -> "ScoreFaixaResposta":
        return cls(faixa_horaria=s.faixa, hora_inicio=s.faixa.hora_inicio, hora_fim=s.faixa.hora_fim,
                   score=s.score, nivel=s.nivel, risco_relativo=s.risco_relativo,
                   taxa_grave_observada=s.taxa_grave_observada, n_sinistros=s.n_sinistros)


class ZonaDetalheResposta(_Dto):
    id: int
    nome: str
    latitude: float
    longitude: float
    faixas: list[ScoreFaixaResposta]

    @classmethod
    def de(cls, z: ZonaDeRisco) -> "ZonaDetalheResposta":
        return cls(id=z.id, nome=z.nome, latitude=z.centro.latitude, longitude=z.centro.longitude,
                   faixas=[ScoreFaixaResposta.de(s) for s in z.scores])


class ImportacaoResposta(_Dto):
    id: int
    arquivo: str
    hash_sha256: str
    total_zonas: int
    total_linhas: int
    criada_em: datetime

    @classmethod
    def de(cls, i: ImportacaoModelo) -> "ImportacaoResposta":
        return cls(id=i.id, arquivo=i.arquivo, hash_sha256=i.hash_sha256, total_zonas=i.total_zonas,
                   total_linhas=i.total_linhas, criada_em=i.criada_em)


class ConsultaResposta(_Dto):
    id: int
    latitude: float
    longitude: float
    hora: int
    faixa_horaria: FaixaHoraria
    zona_id: int | None
    criada_em: datetime

    @classmethod
    def de(cls, c: ConsultaRisco) -> "ConsultaResposta":
        return cls(id=c.id, latitude=c.ponto.latitude, longitude=c.ponto.longitude, hora=c.hora,
                   faixa_horaria=c.faixa, zona_id=c.zona_id, criada_em=c.criada_em)


class ErroResposta(_Dto):
    erro: str = Field(description="Mensagem legível do problema.")
    detalhes: list[str] = Field(default_factory=list)

    model_config = ConfigDict(json_schema_extra={"example": {"erro": "Hora inválida: 25. Use um inteiro de 0 a 23.",
                                                             "detalhes": []}})


class SaudeResposta(_Dto):
    status: str
    banco: str
    zonas: int
    importacao_ativa: int | None
