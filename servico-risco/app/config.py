"""Configuração lida de variáveis de ambiente (ou do arquivo .env). Nada fixo no código."""
from __future__ import annotations

from functools import lru_cache
from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict

RAIZ_SERVICO = Path(__file__).resolve().parent.parent


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=RAIZ_SERVICO / ".env", env_file_encoding="utf-8", extra="ignore")

    # String de conexão SQLAlchemy. Ex.: sqlite:///./guardiao_risco.db ou postgresql+psycopg://user:senha@host/db
    database_url: str = f"sqlite:///{(RAIZ_SERVICO / 'guardiao_risco.db').as_posix()}"
    # Arquivo gerado pelo ML (ml/guardiao_risco_ml.ipynb)
    zonas_csv_path: Path = RAIZ_SERVICO.parent / "ml" / "zonas_risco.csv"
    # Na inicialização: aplica as migrações e, se o banco estiver vazio, importa o CSV
    aplicar_migracoes_na_inicializacao: bool = True
    importar_csv_se_vazio: bool = True
    # Fuso usado quando o app não manda a hora
    fuso_horario: str = "America/Sao_Paulo"
    # Raio padrão da busca de zonas ao redor do motoboy
    raio_padrao_km: float = 2.0


@lru_cache
def get_settings() -> Settings:
    return Settings()
