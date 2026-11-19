"""Caso de uso: carregar uma nova versão das zonas de risco vinda do ML."""
from __future__ import annotations

import hashlib
from datetime import datetime, timezone

from app.domain.entities import ImportacaoModelo
from app.domain.exceptions import ImportacaoDuplicada
from app.domain.repositories import UnidadeDeTrabalho
from app.infrastructure.csv_zonas import ler_zonas


class ImportacaoService:
    def __init__(self, uow: UnidadeDeTrabalho, relogio=lambda: datetime.now(timezone.utc)):
        self._uow = uow
        self._relogio = relogio

    def importar(self, conteudo: bytes, arquivo: str) -> ImportacaoModelo:
        """Valida o CSV inteiro e troca as zonas numa única transação.

        Se qualquer linha for inválida, nada muda no banco. Reenviar o mesmo arquivo da importação
        ativa é recusado, para não criar versões repetidas.
        """
        hash_arquivo = hashlib.sha256(conteudo).hexdigest()
        ativa = self._uow.importacoes.ultima()
        if ativa is not None and ativa.hash_sha256 == hash_arquivo:
            raise ImportacaoDuplicada(f"Este arquivo já é a importação ativa (id {ativa.id}).")

        zonas, total_linhas = ler_zonas(conteudo)
        try:
            importacao = self._uow.importacoes.adicionar(ImportacaoModelo(
                arquivo=arquivo, hash_sha256=hash_arquivo, total_zonas=len(zonas),
                total_linhas=total_linhas, criada_em=self._relogio()))
            self._uow.zonas.substituir_todas(zonas, importacao_id=importacao.id)
            self._uow.commit()
        except Exception:
            self._uow.rollback()
            raise
        return importacao

    def listar(self) -> list[ImportacaoModelo]:
        return self._uow.importacoes.listar()
