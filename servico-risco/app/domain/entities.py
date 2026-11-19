"""Entidades e objetos de valor do domínio de risco.

Nada aqui conhece banco de dados, HTTP ou CSV: são só as regras do negócio.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field
from datetime import datetime

from app.domain.enums import FaixaHoraria, NivelRisco
from app.domain.exceptions import DominioInvalido

RAIO_TERRA_KM = 6371.0


@dataclass(frozen=True)
class Coordenada:
    """Ponto geográfico em WGS84 (o mesmo sistema do GPS do celular e do Infosiga)."""

    latitude: float
    longitude: float

    def __post_init__(self) -> None:
        if not -90 <= self.latitude <= 90:
            raise DominioInvalido(f"Latitude fora do intervalo -90 a 90: {self.latitude}")
        if not -180 <= self.longitude <= 180:
            raise DominioInvalido(f"Longitude fora do intervalo -180 a 180: {self.longitude}")

    def distancia_km(self, outra: "Coordenada") -> float:
        """Distância em linha reta (fórmula de haversine)."""
        lat1, lon1, lat2, lon2 = map(math.radians, (self.latitude, self.longitude, outra.latitude, outra.longitude))
        a = math.sin((lat2 - lat1) / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin((lon2 - lon1) / 2) ** 2
        return 2 * RAIO_TERRA_KM * math.asin(math.sqrt(a))

    def caixa_ao_redor(self, raio_km: float) -> tuple[float, float, float, float]:
        """(lat_min, lat_max, lng_min, lng_max) que contém o círculo de raio `raio_km`.

        Serve para o banco pré-filtrar antes do cálculo exato de distância.
        """
        dlat = math.degrees(raio_km / RAIO_TERRA_KM)
        dlng = math.degrees(raio_km / (RAIO_TERRA_KM * max(math.cos(math.radians(self.latitude)), 1e-6)))
        return self.latitude - dlat, self.latitude + dlat, self.longitude - dlng, self.longitude + dlng


@dataclass(frozen=True)
class ScoreFaixa:
    """Risco de uma zona numa faixa horária, calculado pelo modelo de ML."""

    faixa: FaixaHoraria
    score: float
    risco_relativo: float | None = None
    taxa_grave_observada: float | None = None
    n_sinistros: int | None = None

    def __post_init__(self) -> None:
        if not isinstance(self.score, (int, float)) or math.isnan(self.score) or not 0.0 <= self.score <= 1.0:
            raise DominioInvalido(f"Score deve estar entre 0 e 1: {self.score}")
        if self.taxa_grave_observada is not None and not 0.0 <= self.taxa_grave_observada <= 1.0:
            raise DominioInvalido(f"Taxa observada deve estar entre 0 e 1: {self.taxa_grave_observada}")
        if self.n_sinistros is not None and self.n_sinistros < 0:
            raise DominioInvalido(f"Número de sinistros não pode ser negativo: {self.n_sinistros}")
        if self.risco_relativo is not None and self.risco_relativo < 0:
            raise DominioInvalido(f"Risco relativo não pode ser negativo: {self.risco_relativo}")

    @property
    def nivel(self) -> NivelRisco:
        """O nível nunca é gravado à mão: é sempre derivado do score."""
        return NivelRisco.de_score(self.score)


@dataclass
class ZonaDeRisco:
    """Região de ~2 km x 2 km da cidade com um score para cada faixa horária."""

    id: int
    nome: str
    centro: Coordenada
    _scores: dict[FaixaHoraria, ScoreFaixa] = field(default_factory=dict, repr=False)

    def __post_init__(self) -> None:
        if self.id <= 0:
            raise DominioInvalido(f"Id da zona deve ser positivo: {self.id}")
        self.nome = (self.nome or "").strip()
        if not self.nome:
            raise DominioInvalido("A zona precisa de um nome.")

    @property
    def scores(self) -> list[ScoreFaixa]:
        ordem = list(FaixaHoraria)
        return sorted(self._scores.values(), key=lambda s: ordem.index(s.faixa))

    def adicionar_score(self, score: ScoreFaixa) -> None:
        """Uma zona tem no máximo um score por faixa horária."""
        if score.faixa in self._scores:
            raise DominioInvalido(f"A zona {self.id} já tem score para a faixa {score.faixa.value}.")
        self._scores[score.faixa] = score

    def score_na_faixa(self, faixa: FaixaHoraria) -> ScoreFaixa | None:
        return self._scores.get(faixa)

    def score_na_hora(self, hora: int) -> ScoreFaixa | None:
        return self.score_na_faixa(FaixaHoraria.de_hora(hora))

    def distancia_km(self, ponto: Coordenada) -> float:
        return self.centro.distancia_km(ponto)


@dataclass(frozen=True)
class ZonaConsultada:
    """Resultado de uma consulta: a zona, o score da faixa pedida e a distância até o motoboy."""

    zona: ZonaDeRisco
    score: ScoreFaixa
    distancia_km: float | None


@dataclass
class ImportacaoModelo:
    """Uma carga do arquivo `zonas_risco.csv` gerado pelo ML (versão do modelo em uso)."""

    arquivo: str
    hash_sha256: str
    total_zonas: int
    total_linhas: int
    criada_em: datetime
    id: int | None = None

    def __post_init__(self) -> None:
        if len(self.hash_sha256) != 64:
            raise DominioInvalido("Hash SHA-256 inválido.")
        if self.total_zonas <= 0 or self.total_linhas <= 0:
            raise DominioInvalido("Uma importação precisa ter ao menos uma zona.")


@dataclass
class ConsultaRisco:
    """Registro de auditoria de cada consulta feita pelo app (sem dado pessoal do motoboy)."""

    ponto: Coordenada
    hora: int
    faixa: FaixaHoraria
    zona_id: int | None
    criada_em: datetime
    id: int | None = None
