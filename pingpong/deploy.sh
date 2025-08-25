#!/bin/bash

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}==>${NC} $1"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

# Check if kind cluster exists
check_kind_cluster() {
    print_status "Checking if kind cluster exists..."
    if ! kind get clusters | grep -q "^kind$"; then
        print_error "Kind cluster 'kind' not found. Please run the Egg.K8S bootstrap script first."
        exit 1
    fi
    print_success "Kind cluster found"
}



# Build Docker image
build_image() {
    print_status "Building PingPong Docker image..."
    
    # Build the image (we're already in the pingpong directory)
    docker build -t pingpong:latest .
    
    # Load image into kind cluster
    print_status "Loading image into kind cluster..."
    kind load docker-image pingpong:latest
    
    print_success "Docker image built and loaded into kind cluster"
}

# Deploy to Kubernetes
deploy_to_k8s() {
    print_status "Deploying PingPong instances to Kubernetes..."
    
    # Apply manifests
    print_status "Deploying pinger..."
    kubectl apply -f k8s/pingpong-pinger.yaml
    
    print_status "Deploying ponger..."
    kubectl apply -f k8s/pingpong-ponger.yaml
    
    print_success "Deployments applied"
}

# Wait for deployments to be ready
wait_for_deployments() {
    print_status "Waiting for deployments to be ready..."
    
    # Wait for pinger
    print_status "Waiting for pingpong-pinger..."
    kubectl wait --for=condition=available --timeout=300s deployment/pingpong-pinger
    
    # Wait for ponger
    print_status "Waiting for pingpong-ponger..."
    kubectl wait --for=condition=available --timeout=300s deployment/pingpong-ponger
    
    print_success "All deployments are ready"
}

# Show deployment status
show_status() {
    print_status "Deployment status:"
    echo ""
    
    # Show pods
    kubectl get pods -l app=pingpong-pinger
    kubectl get pods -l app=pingpong-ponger
    
    echo ""
    print_status "Services:"
    kubectl get svc -l app=pingpong-pinger
    kubectl get svc -l app=pingpong-ponger
    
    echo ""
    print_status "Ingress endpoints:"
    echo "Pinger: http://pingpong-pinger.localhost"
    echo "Ponger: http://pingpong-ponger.localhost"
    
    echo ""
    print_status "For Telepresence interception:"
    echo "Pinger: telepresence intercept pingpong-pinger --port 8080:8080"
    echo "Ponger: telepresence intercept pingpong-ponger --port 8080:8080"
}

# Test the deployments
test_deployments() {
    print_status "Testing deployments..."
    
    # Wait a bit for services to be ready
    sleep 5
    
    # Test ponger health (should always work as it's PONG ONLY)
    print_status "Testing ponger health..."
    if kubectl run test-ponger --image=busybox --rm -it --restart=Never -- wget -qO- http://pingpong-ponger:8080/health; then
        print_success "Ponger health check passed"
    else
        print_error "Ponger health check failed"
    fi
}

# Cleanup function
cleanup() {
    print_status "Cleaning up deployments..."
    kubectl delete -f k8s/pingpong-pinger.yaml --ignore-not-found=true
    kubectl delete -f k8s/pingpong-ponger.yaml --ignore-not-found=true
    print_success "Cleanup completed"
}



# Main script
main() {
    echo "🚀 PingPong Kubernetes Deployment Script"
    echo "========================================"
    echo ""
    
    # Parse command line arguments
    case "${1:-deploy}" in
        "deploy")
            check_kind_cluster
            cleanup
            build_image
            deploy_to_k8s
            wait_for_deployments
            show_status
            test_deployments
            ;;
        "cleanup")
            cleanup
            ;;
        "status")
            show_status
            ;;
        "test")
            test_deployments
            ;;
        *)
            echo "Usage: $0 [deploy|cleanup|status|test]"
            echo ""
            echo "Commands:"
            echo "  deploy  - Clean deploy PingPong instances (default)"
            echo "  cleanup - Remove PingPong deployments"
            echo "  status  - Show deployment status"
            echo "  test    - Test the deployments"
            exit 1
            ;;
    esac
}

# Run main function
main "$@"
