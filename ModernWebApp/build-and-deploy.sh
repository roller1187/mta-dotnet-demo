#!/bin/bash
# Build and deploy to OpenShift

set -e

# Variables
APP_NAME="modern-webapp"
NAMESPACE="${OPENSHIFT_NAMESPACE:-your-namespace}"
IMAGE_NAME="${APP_NAME}:latest"

echo "Building container image using Red Hat UBI..."

# Login to OpenShift (if not already logged in)
echo "Make sure you're logged in to OpenShift: oc login"

# Create namespace if it doesn't exist
oc new-project ${NAMESPACE} 2>/dev/null || true

# Option 1: Build with Podman locally using Containerfile
# podman build -f Containerfile -t ${IMAGE_NAME} .
# podman tag ${IMAGE_NAME} image-registry.openshift-image-registry.svc:5000/${NAMESPACE}/${IMAGE_NAME}
# podman push image-registry.openshift-image-registry.svc:5000/${NAMESPACE}/${IMAGE_NAME}

# Option 2: Use OpenShift's built-in build (Recommended)
echo "Creating build configuration in OpenShift..."
oc new-build --name=${APP_NAME} --binary --strategy=docker 2>/dev/null || echo "Build config already exists"

echo "Starting build from Containerfile..."
oc start-build ${APP_NAME} --from-dir=. --follow

echo "Creating application deployment..."
oc new-app ${APP_NAME} 2>/dev/null || echo "App already exists"

# Or apply the deployment YAML
# sed "s/your-namespace/${NAMESPACE}/g" openshift-deployment.yaml | oc apply -f -

# Expose the service
oc expose service/${APP_NAME} || echo "Route already exists"

# Get the route URL
echo ""
echo "Deployment complete!"
echo "Application URL:"
oc get route ${APP_NAME} -o jsonpath='{.spec.host}'
echo ""
