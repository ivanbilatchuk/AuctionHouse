#!/usr/bin/env bash
# Розгортає всі три бази AuctionHouse (Catalog, Auctions, Community) у контейнерах Docker.
# Передумова: docker compose -f scripts/docker-compose.db.yml up -d
# Усі файли ідемпотентні -- скрипт можна запускати повторно.
# Використання:  ./scripts/deploy.sh [--verify]
set -euo pipefail

SA_PASSWORD="${MSSQL_SA_PASSWORD:-AuctionHouse_Dev#2026}"
SQL_CONTAINER="${SQL_CONTAINER:-auctionhouse-sql}"
MONGO_CONTAINER="${MONGO_CONTAINER:-auctionhouse-mongo}"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd

run_sql() {
    echo ">> SQL   $1"
    docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -I -b -f 65001 -i "/scripts/$1"
}

run_mongo() {
    echo ">> Mongo $1"
    docker exec "$MONGO_CONTAINER" mongosh --quiet "mongodb://localhost:27017" --file "/scripts/$1"
}

echo "Очікування SQL Server..."
ready=0
for _ in $(seq 1 40); do
    if docker exec "$SQL_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
        ready=1; break
    fi
    sleep 3
done
if [ "$ready" -ne 1 ]; then
    echo "SQL Server не відповідає. Перевірте: docker compose -f scripts/docker-compose.db.yml ps" >&2
    exit 1
fi

# Catalog (Проєкт №2)
run_sql p2/schema.sql
run_sql p2/seed.sql

# Auctions (Проєкт №1)
run_sql p1/schema.sql
run_sql p1/procedures.sql
run_sql p1/seed.sql

# Community (Проєкт №3)
run_mongo p3/collections.js
run_mongo p3/seed.js

if [ "${1:-}" = "--verify" ]; then
    run_sql p1/verify.sql
    run_mongo p3/queries.js
fi

echo "Готово: AuctionHouse_Catalog, AuctionHouse_Auctions, AuctionHouse_Community розгорнуто."
