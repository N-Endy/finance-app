FROM node:22-alpine
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install
COPY . .
ARG FINANCEOS_API_ORIGIN=http://localhost:5080
ENV FINANCEOS_API_ORIGIN=$FINANCEOS_API_ORIGIN
ENV NEXT_PUBLIC_API_URL=
RUN npm run build
EXPOSE 3000
CMD ["sh", "-c", "npx next start --port ${PORT:-3000}"]
