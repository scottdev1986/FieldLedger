FROM node:26.5.0-alpine AS deps
WORKDIR /app
RUN npm install -g npm@12.0.0
COPY package.json package-lock.json ./
RUN npm ci

FROM node:26.5.0-alpine AS build
WORKDIR /app
RUN npm install -g npm@12.0.0
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:26.5.0-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
RUN npm install -g npm@12.0.0
COPY --from=build /app/package.json ./package.json
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
EXPOSE 3000
CMD ["npm", "run", "start"]
