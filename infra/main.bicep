@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('GHCR image to deploy, e.g. ghcr.io/slhote/planit-api:abc1234.')
param containerImage string

@secure()
@description('Neon connection string (direct, non-pooled). Stored as a Container App secret.')
param neonConnectionString string

@secure()
@description('JWT HS256 signing key (32+ random characters).')
param jwtSigningKey string

// ── Log Analytics (required by Container Apps Environment) ──────────────────

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'planit-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Container Apps Environment ──────────────────────────────────────────────

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'planit-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ── Container App ───────────────────────────────────────────────────────────

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'planit-api'
  location: location
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        // Sticky sessions keep a client pinned to one replica, preserving in-process SignalR
        // group membership. Single-revision deployment makes this a no-op in practice, but it's
        // the right default once scale-out is enabled.
        stickySessions: { affinity: 'sticky' }
      }
      secrets: [
        { name: 'neon-connection-string', value: neonConnectionString }
        { name: 'jwt-signing-key', value: jwtSigningKey }
      ]
    }
    template: {
      containers: [
        {
          name: 'planit-api'
          image: containerImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ConnectionStrings__DefaultConnection', secretRef: 'neon-connection-string' }
            { name: 'Jwt__SigningKey', secretRef: 'jwt-signing-key' }
            { name: 'Jwt__Issuer', value: 'PlanIt' }
            { name: 'Jwt__Audience', value: 'PlanIt' }
            { name: 'Jwt__ExpirationMinutes', value: '15' }
            { name: 'Jwt__RefreshTokenExpirationDays', value: '7' }
            { name: 'Cors__AllowedOrigins__0', value: 'https://slhote.github.io' }
            // Set APPLY_MIGRATIONS=true on first deploy to run EF Core migrations at startup.
            // Remove after the first successful deploy — not needed on subsequent deploys unless
            // a new migration is added (in which case set it again for that deploy only).
            { name: 'APPLY_MIGRATIONS', value: 'false' }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────

@description('The public FQDN of the Container App (use as VITE_API_BASE_URL in GitHub Actions).')
output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
