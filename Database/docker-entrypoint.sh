#!/usr/bin/env bash
set -Eeuo pipefail

readonly database_name="dovizDb"

if [[ -x /opt/mssql-tools18/bin/sqlcmd ]]; then
    readonly sqlcmd_bin=/opt/mssql-tools18/bin/sqlcmd
else
    readonly sqlcmd_bin=/opt/mssql-tools/bin/sqlcmd
fi

/opt/mssql/bin/sqlservr &
sqlserver_pid=$!

shutdown_sqlserver() {
    kill -TERM "$sqlserver_pid" 2>/dev/null || true
    wait "$sqlserver_pid" 2>/dev/null || true
}
trap shutdown_sqlserver SIGINT SIGTERM

sqlcmd_args=(
    -S localhost
    -U sa
    -P "$MSSQL_SA_PASSWORD"
    -C
    -b
    -l 5
)

sqlserver_hazir=false
for _ in {1..60}; do
    if "$sqlcmd_bin" "${sqlcmd_args[@]}" -d master -Q "SELECT 1" >/dev/null 2>&1; then
        sqlserver_hazir=true
        break
    fi

    if ! kill -0 "$sqlserver_pid" 2>/dev/null; then
        wait "$sqlserver_pid"
        exit $?
    fi

    sleep 2
done

if [[ "$sqlserver_hazir" != true ]]; then
    echo "SQL Server 120 saniye içinde hazır hale gelmedi." >&2
    shutdown_sqlserver
    exit 1
fi

"$sqlcmd_bin" "${sqlcmd_args[@]}" -d master -Q \
    "IF DB_ID(N'$database_name') IS NULL CREATE DATABASE [$database_name];"

cekirdek_tablo_var=$(
    "$sqlcmd_bin" "${sqlcmd_args[@]}" -d "$database_name" -h -1 -W -Q \
        "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.Dovizler', N'U') IS NULL THEN 0 ELSE 1 END;" \
        | tr -d '\r[:space:]'
)

if [[ "$cekirdek_tablo_var" == "0" ]]; then
    echo "Döviz veritabanı ilk kez hazırlanıyor..."
    "$sqlcmd_bin" "${sqlcmd_args[@]}" -d "$database_name" \
        -i /docker-entrypoint-initdb.d/005_FullDatabaseSetup.sql
else
    echo "Çekirdek tablolar mevcut; tam kurulum scripti atlanıyor."
fi

"$sqlcmd_bin" "${sqlcmd_args[@]}" -d "$database_name" \
    -i /docker-entrypoint-initdb.d/006_AddDovizIslemiTersKayit.sql
"$sqlcmd_bin" "${sqlcmd_args[@]}" -d "$database_name" \
    -i /docker-entrypoint-initdb.d/007_AddHataLoglari.sql

echo "Döviz veritabanı hazır."
wait "$sqlserver_pid"
