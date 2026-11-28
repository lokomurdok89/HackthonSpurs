# Keep the existing demo's PostgreSQL build, libc, locale and data layout.
ARG POSTGRES_IMAGE=postgres:18.4-alpine3.23@sha256:996d0920e4ff9df1fc19dacb904492f3c1ec0ec1cc338f0ad7123be7731c5f5e
FROM ${POSTGRES_IMAGE} AS extension
RUN apk add --no-cache build-base
# pgvector v0.8.2, pinned by upstream commit and archive checksum.
ADD --checksum=sha256:0af705b6c9b2a3a1fd1f6fd991425c125387d3c8edc68b75ef027d2e0a05e57a https://codeload.github.com/pgvector/pgvector/tar.gz/cab9da72c04353f143bb06b42ab70a403daac64a /tmp/vector.tar.gz
RUN mkdir /src && tar xzf /tmp/vector.tar.gz -C /src --strip-components=1 \
    && make -C /src with_llvm=no OPTFLAGS="" \
    && make -C /src with_llvm=no OPTFLAGS="" install DESTDIR=/out

FROM ${POSTGRES_IMAGE}
COPY --from=extension /out/usr/local/lib/postgresql/vector.so /usr/local/lib/postgresql/vector.so
COPY --from=extension /out/usr/local/share/postgresql/extension/ /usr/local/share/postgresql/extension/
COPY --from=extension /src/LICENSE /usr/local/share/licenses/pgvector/LICENSE
COPY 10-enable-vector.sql /docker-entrypoint-initdb.d/10-enable-vector.sql
# Both a fresh named volume and the existing demo data directory are owned by postgres.
USER postgres
