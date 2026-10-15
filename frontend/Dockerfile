FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
# npm ci postinstall sırasında MapLibre worker kopyalama betiğini çalıştırır.
COPY scripts/ scripts/
RUN npm ci

FROM deps AS build
ARG NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
COPY . .
RUN npm run build
# Bu Next.js sürümü next.config.ts dosyasını kendi SWC yükleyicisiyle okur; dev paketleri çalışma zamanı için gerekmez.
RUN npm prune --omit=dev

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
EXPOSE 3000
# Next.js çalışma zamanı önbelleğine node kullanıcısı yazabilmelidir.
COPY --from=build --chown=node:node /app/.next ./.next
COPY --from=build --chown=node:node /app/public ./public
COPY --from=build --chown=node:node /app/node_modules ./node_modules
COPY --from=build --chown=node:node /app/package.json ./package.json
COPY --from=build --chown=node:node /app/messages ./messages
COPY --from=build --chown=node:node /app/next.config.ts ./next.config.ts
COPY --from=build --chown=node:node /app/tsconfig.json ./tsconfig.json
COPY --from=build --chown=node:node /app/src/i18n ./src/i18n
USER node
CMD ["npm", "start"]
