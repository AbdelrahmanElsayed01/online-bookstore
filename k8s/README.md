## Local Kubernetes (Minikube) quickstart

1. **Start Minikube**
   ```bash
   minikube start
   kubectl config set-context --current --namespace=online-bookstore
   ```

2. **Load Docker images into the cluster**
   ```bash
   eval "$(minikube -p minikube docker-env)"

   docker build -t catalog-service:local \
     -f services/catalog-service/CatalogService/Dockerfile \
     services/catalog-service

   docker build -t order-service:local \
     -f services/OrderService/Dockerfile \
     services

   docker build -t api-gateway:local \
     -f services/api-gateway/ApiGateway/Dockerfile \
     services/api-gateway/ApiGateway

   docker build -t payment-service:local \
     -f services/payment-service/PaymentService/Dockerfile \
     services/payment-service/PaymentService
   ```
   > Run `eval "$(minikube docker-env -u)"` when you're done building images.

3. **Create namespace, config, and secrets**
   ```bash
   kubectl apply -f k8s/namespace.yaml
   kubectl apply -f k8s/configmap.yaml
   ```
   Replace the placeholders inside `k8s/configmap.yaml` before applying or run:
   ```bash
   kubectl create secret generic bookstore-secrets \
     --namespace online-bookstore \
     --from-env-file=.env \
     --dry-run=client -o yaml | kubectl apply -f -
   ```

4. **Deploy services**
   ```bash
   kubectl apply -f k8s/catalog-service.yaml
   kubectl apply -f k8s/order-service.yaml
   kubectl apply -f k8s/payment-service.yaml
   kubectl apply -f k8s/api-gateway.yaml
   ```
   Check status with `kubectl get pods`.

5. **Expose the API Gateway**
   - NodePort: `minikube service api-gateway -n online-bookstore --url`
   - Ingress (optional): `minikube addons enable ingress`, edit `/etc/hosts` to map `bookstore.local` to `$(minikube ip)`, then `kubectl apply -f k8s/ingress.yaml`.

6. **Iterate**
   ```bash
   docker build -t catalog-service:local -f services/catalog-service/CatalogService/Dockerfile services/catalog-service
   kubectl rollout restart deployment/catalog-service
   ```
   Repeat for the other services whenever you make code changes.

## Optional: Loki + Grafana monitoring

Collect logs from every pod and view them in Grafana:

1. Deploy the monitoring stack (Loki, Promtail, Grafana):
   ```bash
   kubectl apply -f k8s/monitoring/namespace.yaml
   kubectl apply -f k8s/monitoring/loki-config.yaml
   kubectl apply -f k8s/monitoring/loki.yaml
   kubectl apply -f k8s/monitoring/promtail-config.yaml
   kubectl apply -f k8s/monitoring/promtail.yaml
   kubectl apply -f k8s/monitoring/grafana.yaml
   ```
2. Open Grafana (default admin/admin):
   ```bash
   minikube service grafana -n monitoring --url
   ```
   or `kubectl port-forward svc/grafana 4000:80 -n monitoring`.
3. In Grafana’s Explore view, pick the “Loki” datasource and either:
   - Use the **Builder**: add a label filter like `namespace = online-bookstore`, then optionally filter by `app`/`pod` or add a “Line contains” clause.
   - Or switch to **Code** mode and type a LogQL query directly, e.g.:
     ```
     {namespace="online-bookstore"} |= "Order"
     ```
4. Run the query to stream logs; add more label filters if you see “no options” (labels only appear after Promtail has sent at least one log from that namespace/app).
