FROM node:24.18.0-alpine3.23 AS build
WORKDIR /ui
COPY package*.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

FROM nginx:1.29.6-alpine
COPY nginx.conf /etc/nginx/nginx.conf
COPY --from=build /ui/dist /srv/web
USER nginx
ENTRYPOINT ["nginx"]
CMD ["-g", "daemon off;"]
