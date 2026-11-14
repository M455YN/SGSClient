# Szyr Game Studio website (sgs.m455yn.io) with the SGSClient docs under /docs/.
# Build from the repository root:  docker build -f website/Dockerfile -t sgs-website .

FROM node:22-alpine AS css
WORKDIR /web
COPY website/package.json ./
RUN npm install --no-audit --no-fund
COPY website/src ./src
COPY website/public ./public
RUN npm run build

FROM python:3.12-slim AS docs
WORKDIR /repo
RUN pip install --no-cache-dir zensical==0.0.74
COPY zensical.toml ./
COPY docs ./docs
RUN zensical build --clean

FROM nginx:1.27-alpine
COPY website/nginx.conf /etc/nginx/conf.d/default.conf
COPY website/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=css /web/public /usr/share/nginx/html
COPY --from=docs /repo/site /usr/share/nginx/html/docs
EXPOSE 80
