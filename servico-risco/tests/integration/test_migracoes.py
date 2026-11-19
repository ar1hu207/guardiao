"""As duas migrações sobem e descem sem erro, e cada uma cria o que promete."""
from sqlalchemy import create_engine, inspect

from app.infrastructure.migracoes import aplicar_migracoes, reverter_migracoes


def _esquema(url):
    engine = create_engine(url)
    try:
        insp = inspect(engine)
        tabelas = set(insp.get_table_names()) - {"alembic_version"}
        colunas_scores = {c["name"] for c in insp.get_columns("scores_zona")} if "scores_zona" in tabelas else set()
        return tabelas, colunas_scores
    finally:
        engine.dispose()


def test_migracao_inicial_cria_zonas_e_scores(database_url):
    aplicar_migracoes(database_url, "0001_inicial")
    tabelas, colunas = _esquema(database_url)
    assert tabelas == {"zonas", "scores_zona"}
    assert "importacao_id" not in colunas


def test_segunda_migracao_evolui_o_esquema(database_url):
    aplicar_migracoes(database_url, "0001_inicial")
    aplicar_migracoes(database_url, "head")
    tabelas, colunas = _esquema(database_url)
    assert tabelas == {"zonas", "scores_zona", "importacoes_modelo", "consultas_risco"}
    assert "importacao_id" in colunas


def test_migracoes_descem_e_sobem_de_novo(database_url):
    aplicar_migracoes(database_url)
    reverter_migracoes(database_url, "0001_inicial")
    assert _esquema(database_url)[0] == {"zonas", "scores_zona"}
    reverter_migracoes(database_url, "base")
    assert _esquema(database_url)[0] == set()
    aplicar_migracoes(database_url)
    assert len(_esquema(database_url)[0]) == 4
