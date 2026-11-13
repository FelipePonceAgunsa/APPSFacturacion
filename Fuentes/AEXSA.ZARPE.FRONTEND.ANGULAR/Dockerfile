FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json .npmrc ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/aexsa.zarpe.frontend.angular/browser /usr/share/nginx/html

# El tag de la imagen queda horneado como asset estatico: la app y
# /assets/version.json siempre reportan la version que realmente corre.
ARG IMAGE_TAG=local
RUN mkdir -p /usr/share/nginx/html/assets \
    && printf '{"version":"%s"}' "$IMAGE_TAG" > /usr/share/nginx/html/assets/version.json

EXPOSE 80
