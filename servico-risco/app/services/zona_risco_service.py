"""Casos de uso de consulta de risco."""
from __future__ import annotations

from datetime import datetime, timezone

from app.domain.entities import ConsultaRisco, Coordenada, ZonaConsultada, ZonaDeRisco
from app.domain.enums import FaixaHoraria
from app.domain.exceptions import DominioInvalido, ZonaNaoEncontrada
from app.domain.repositories import UnidadeDeTrabalho

RAIO_MAXIMO_KM = 20.0
LIMITE_MAXIMO = 50


class ZonaRiscoService:
    def __init__(self, uow: UnidadeDeTrabalho, relogio=lambda: datetime.now(timezone.utc)):
        self._uow = uow
        self._relogio = relogio

    def consultar_proximas(self, ponto: Coordenada, hora: int, raio_km: float, limite: int) -> list[ZonaConsultada]:
        """Zonas num raio ao redor do motoboy, na faixa da hora pedida, da mais próxima para a mais distante.

        Toda consulta fica registrada (auditoria), mesmo sem zona encontrada.
        """
        faixa = FaixaHoraria.de_hora(hora)
        self._validar_busca(raio_km, limite)

        candidatas = self._uow.zonas.listar_na_caixa(*ponto.caixa_ao_redor(raio_km), faixa=faixa)
        resultado = sorted(
            (ZonaConsultada(zona=z, score=z.score_na_faixa(faixa), distancia_km=round(z.distancia_km(ponto), 3))
             for z in candidatas if z.distancia_km(ponto) <= raio_km),
            key=lambda r: r.distancia_km,
        )[:limite]

        self._uow.consultas.adicionar(ConsultaRisco(
            ponto=ponto, hora=hora, faixa=faixa,
            zona_id=resultado[0].zona.id if resultado else None, criada_em=self._relogio()))
        self._uow.commit()
        return resultado

    def ranking(self, hora: int, limite: int) -> list[ZonaConsultada]:
        """Zonas mais arriscadas da cidade na faixa da hora pedida (para o mapa do app)."""
        faixa = FaixaHoraria.de_hora(hora)
        self._validar_busca(RAIO_MAXIMO_KM, limite)
        return [ZonaConsultada(zona=z, score=z.score_na_faixa(faixa), distancia_km=None)
                for z in self._uow.zonas.ranking(faixa, limite)]

    def obter(self, zona_id: int) -> ZonaDeRisco:
        zona = self._uow.zonas.obter(zona_id)
        if zona is None:
            raise ZonaNaoEncontrada(zona_id)
        return zona

    def consultas_recentes(self, limite: int) -> list[ConsultaRisco]:
        self._validar_busca(RAIO_MAXIMO_KM, limite)
        return self._uow.consultas.listar_recentes(limite)

    @staticmethod
    def _validar_busca(raio_km: float, limite: int) -> None:
        if not 0 < raio_km <= RAIO_MAXIMO_KM:
            raise DominioInvalido(f"O raio deve ser maior que 0 e no máximo {RAIO_MAXIMO_KM} km.")
        if not 1 <= limite <= LIMITE_MAXIMO:
            raise DominioInvalido(f"O limite deve estar entre 1 e {LIMITE_MAXIMO}.")
