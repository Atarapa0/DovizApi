FROM mcr.microsoft.com/mssql/server:2022-latest

USER root
COPY Database/005_FullDatabaseSetup.sql /docker-entrypoint-initdb.d/005_FullDatabaseSetup.sql
COPY Database/006_AddDovizIslemiTersKayit.sql /docker-entrypoint-initdb.d/006_AddDovizIslemiTersKayit.sql
COPY Database/007_AddHataLoglari.sql /docker-entrypoint-initdb.d/007_AddHataLoglari.sql
COPY Database/docker-entrypoint.sh /usr/local/bin/doviz-db-entrypoint
RUN chmod 755 /usr/local/bin/doviz-db-entrypoint \
    && chown -R mssql:root /docker-entrypoint-initdb.d

USER mssql
ENTRYPOINT ["/usr/local/bin/doviz-db-entrypoint"]
