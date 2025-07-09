# ReadAIrr Docker Container
FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine

# Install dependencies
RUN apk add --no-cache curl ca-certificates

# Create app directory
WORKDIR /app

# Create readairr user and group
RUN addgroup -g 1000 readairr && \
    adduser -D -u 1000 -G readairr readairr

# Copy the published application
COPY _output/net6.0/linux-x64/publish/ ./

# Create directories for ReadAIrr data
RUN mkdir -p /config /books /downloads && \
    chown -R readairr:readairr /app /config /books /downloads

# Switch to readairr user
USER readairr

# Expose ports
EXPOSE 8246

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8246/api/v1/system/status || exit 1

# Set environment variables
ENV READAIRR__INSTANCENAME="ReadAIrr (Docker)" \
    READAIRR__BRANCH="main" \
    READAIRR__APIKEY="" \
    READAIRR__LOGLEVEL="info"

# Start ReadAIrr
ENTRYPOINT ["./Readarr", "-nobrowser", "-data=/config"]
