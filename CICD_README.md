# CI/CD Pipeline Documentation

This document describes the CI/CD pipeline setup for the Online Bookstore project.

## Overview

The CI/CD pipeline is built using GitHub Actions and supports:
- **Frontend**: Next.js application with automated build and test
- **Backend**: Two .NET 8.0 microservices (Catalog Service and Order Service)
- **Containerization**: Docker builds and pushes to GitHub Container Registry
- **Testing**: Automated unit tests and load testing with k6
- **Security**: Vulnerability scanning with Trivy
- **Deployment**: Multiple deployment options (Docker Compose, Kubernetes)

## Pipeline Stages

### 1. Frontend Pipeline
- **Trigger**: Push to main/develop branches, pull requests
- **Steps**:
  - Checkout code
  - Setup Node.js 18
  - Install dependencies
  - Run linting (if configured)
  - Build application
  - Run tests (if configured)

### 2. Backend Pipeline
- **Trigger**: Push to main/develop branches, pull requests
- **Steps**:
  - Checkout code
  - Setup .NET 8.0
  - Restore dependencies for both services
  - Build solutions
  - Run unit tests

### 3. Docker Build & Push
- **Trigger**: Push to main/develop branches only
- **Steps**:
  - Build Docker images for both services
  - Push to GitHub Container Registry (ghcr.io)
  - Multi-arch support with caching

### 4. Load Testing
- **Trigger**: Push to main branch only
- **Steps**:
  - Setup k6 load testing tool
  - Start services using docker-compose
  - Run performance tests
  - Cleanup services

### 5. Security Scanning
- **Trigger**: Pull requests
- **Steps**:
  - Run Trivy vulnerability scanner
  - Upload results to GitHub Security tab

### 6. Deployment
- **Trigger**: Push to main branch only
- **Steps**:
  - Deploy to production environment
  - Run health checks

## Setup Instructions

### 1. GitHub Secrets
Configure the following secrets in your GitHub repository:

```bash
# Required secrets (set in GitHub repository settings)
GITHUB_TOKEN          # Automatically provided by GitHub
JWT_SECRET            # Your JWT secret key
JWT_ISSUER            # Your Supabase JWT issuer URL
JWT_AUDIENCE          # Your JWT audience
NEXT_PUBLIC_SUPABASE_URL      # Your Supabase URL
NEXT_PUBLIC_SUPABASE_ANON_KEY # Your Supabase anon key
```

### 2. Environment Configuration
Copy the environment template and update with your values:

```bash
cp env.example .env
# Edit .env with your actual values
```

### 3. Container Registry
The pipeline uses GitHub Container Registry. Images will be available at:
- `ghcr.io/your-username/online-bookstore/catalog-service:latest`
- `ghcr.io/your-username/online-bookstore/order-service:latest`

### 4. Update Image Names
In the workflow file (`.github/workflows/ci-cd.yml`), update the `IMAGE_NAME` environment variable:
```yaml
env:
  IMAGE_NAME: ${{ github.repository }}  # This will be your-username/online-bookstore
```

## Deployment Options

### Option 1: Docker Compose (Recommended for simple deployments)
```bash
# Production deployment
docker-compose -f docker-compose.prod.yml up -d
```

### Option 2: Kubernetes (Recommended for production)
```bash
# Apply Kubernetes manifests
kubectl apply -f k8s/
```

### Option 3: Cloud Provider
The pipeline includes placeholder steps for cloud deployments. Customize the deployment step based on your cloud provider:
- **AWS**: ECS, EKS, or EC2
- **Google Cloud**: Cloud Run, GKE, or Compute Engine
- **Azure**: Container Instances, AKS, or App Service

## Monitoring and Health Checks

### Health Check Endpoints
Both services should implement health check endpoints:
- Catalog Service: `GET /health`
- Order Service: `GET /health`
- Frontend: `GET /api/health`

### Logging
Services should implement structured logging for production monitoring.

## Load Testing

The pipeline includes k6 load testing scripts:
- `10VUs-30secs.js`: Light load test
- `100VUs-30secs.js`: Heavy load test
- `CRUD50VUs-120secs.js`: CRUD operations test

Customize these scripts based on your performance requirements.

## Security Considerations

1. **Secrets Management**: All sensitive data is stored in GitHub Secrets
2. **Vulnerability Scanning**: Trivy scans for known vulnerabilities
3. **Container Security**: Multi-stage Docker builds with non-root users
4. **Network Security**: Kubernetes network policies (optional)

## Troubleshooting

### Common Issues

1. **Build Failures**: Check .NET version compatibility and package references
2. **Docker Build Failures**: Verify Dockerfile syntax and base images
3. **Test Failures**: Ensure all dependencies are properly installed
4. **Deployment Failures**: Check environment variables and service configurations

### Debug Commands

```bash
# Check pipeline logs in GitHub Actions
# View container logs
docker-compose logs -f [service-name]

# Check Kubernetes pods
kubectl get pods -n online-bookstore
kubectl logs [pod-name] -n online-bookstore
```

## Customization

### Adding New Services
1. Create Dockerfile for the new service
2. Add build steps in the backend job matrix
3. Add Docker build steps in docker-build job
4. Update deployment configurations

### Adding New Tests
1. Add test scripts to the appropriate service
2. Update the test commands in the pipeline
3. Configure test result reporting

### Environment-Specific Configurations
Create environment-specific configuration files:
- `docker-compose.staging.yml`
- `k8s/staging/`
- Environment-specific secrets and configmaps

## Performance Optimization

1. **Build Caching**: Pipeline uses GitHub Actions cache and Docker layer caching
2. **Parallel Jobs**: Frontend and backend builds run in parallel
3. **Resource Limits**: Kubernetes deployments include resource requests and limits
4. **Load Testing**: Automated performance validation

## Maintenance

1. **Regular Updates**: Keep base images and dependencies updated
2. **Security Patches**: Monitor vulnerability scan results
3. **Performance Monitoring**: Review load test results regularly
4. **Log Analysis**: Monitor application logs for issues

## Support

For issues with the CI/CD pipeline:
1. Check GitHub Actions logs
2. Review this documentation
3. Check service-specific logs
4. Verify environment configurations
