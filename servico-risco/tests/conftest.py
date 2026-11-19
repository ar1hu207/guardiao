from __future__ import annotations

from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.config import Settings
from app.main import create_app

FIXTURES = Path(__file__).parent / "fixtures"
CSV_TESTE = FIXTURES / "zonas_teste.csv"


@pytest.fixture
def database_url(tmp_path: Path) -> str:
    """Cada teste ganha um banco SQLite novo, num arquivo temporário."""
    return f"sqlite:///{(tmp_path / 'teste.db').as_posix()}"


@pytest.fixture
def settings(database_url: str) -> Settings:
    return Settings(database_url=database_url, zonas_csv_path=CSV_TESTE)


@pytest.fixture
def client(settings: Settings):
    """API real: aplica as migrações e importa o CSV de teste na inicialização."""
    with TestClient(create_app(settings)) as c:
        yield c
