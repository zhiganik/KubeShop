# Lesson 1 — Cluster, Pods, Labels, ConfigMaps, Secrets, Deployments, Logs

All commands are for **PowerShell**, run from the repository root.
**You write the manifests yourself** in `deploy/k8s/`. Each step has an imperative command to try first,
then a ✍️ task with the requirements for the YAML version. Two helpers do most of the work:

```powershell
kubectl <create|run> ... --dry-run=client -o yaml   # prints a YAML skeleton instead of creating anything
kubectl explain <kind>.<field> [--recursive]        # built-in docs for every field
```

## The app in one picture

```
 you ──POST /orders──▶ Orders.Api ──GET /products/{id}──▶ Catalog.Api
                          ▲
                          └──GET /orders (every 5s)── Notifications.Worker ──▶ logs "Notification sent…"
```

| Service | Settings it needs | Where they come from in k8s |
|---|---|---|
| Catalog.Api | none | — |
| Orders.Api | `Catalog:BaseUrl`, `Orders:MaxQuantity`, **`Orders:ApiKey`** | ConfigMap + **Secret** |
| Notifications.Worker | `Orders:BaseUrl`, `Notifications:PollSeconds` | ConfigMap |

Orders keeps orders **in memory**. That's deliberate, and you'll see why it matters once there are 2 replicas.
Orders **crashes on startup without an API key**. That's deliberate too, so you can practise on a CrashLoopBackOff.

---

## 0. What is a cluster?

A Kubernetes cluster is a set of machines (**nodes**) plus a **control plane** that manages them.

```
                ┌──────────── Control plane ────────────┐
  kubectl ────▶ │ kube-apiserver  ◀── the ONLY entry point │
  (HTTPS+YAML)  │ etcd            ── database of "desired state" │
                │ scheduler       ── picks a node for each pod   │
                │ controller-mgr  ── loops: "actual → desired"   │
                └──────────────────────────────────────────┘
                         │
          ┌──────────────┴──────────────┐
     ┌── Node ──────────┐         ┌── Node ──────────┐
     │ kubelet           │         │ kubelet           │  ← starts/stops containers on its node
     │ container runtime │         │ container runtime │
     │ kube-proxy        │         │ kube-proxy        │  ← Service networking
     │  [pod] [pod]      │         │  [pod]            │
     └───────────────────┘         └───────────────────┘
```

Key ideas:
- **kubectl doesn't create clusters.** It's just an HTTP client for the API server of a cluster that already exists.
- The YAML you `kubectl apply` describes objects **inside** a cluster (pods, deployments…), not the cluster itself.
- You declare the *desired state*. Controllers keep comparing it with the *actual state* and fixing the difference. That loop is the core idea of Kubernetes.
- kubectl knows which cluster to talk to from `~/.kube/config` (the **kubeconfig**). Each cluster + user + namespace combination is a **context**.

### Ways to get a cluster

| Option | How you create it | Good for |
|---|---|---|
| **Docker Desktop** ✅ *use this today* | A checkbox in the GUI | Simplest. With the kubeadm setting it can use the images you built locally |
| **kind** (Kubernetes in Docker) | CLI + a YAML **cluster config** (`kind create cluster --config kind.yaml`) | Multi-node clusters, throw-away clusters, CI. This is the "cluster from YAML" option |
| minikube | CLI | Similar to kind, has many add-ons |
| AKS / EKS / GKE | Cloud CLI (`az aks create …`) or Terraform | Real environments, later |

So "console vs YAML vs Docker Desktop" is really two separate choices:
1. **How the cluster is created:** Docker Desktop GUI, kind/minikube CLI (optionally with a YAML config), or a cloud.
2. **How you manage things *inside* it:** *imperative* commands (`kubectl run`, `kubectl create`) or *declarative* YAML (`kubectl apply -f`). You'll learn both today. Real projects use YAML.

### Enable Kubernetes in Docker Desktop

1. Docker Desktop → **Settings → Kubernetes → Enable Kubernetes**.
2. If you're asked for a cluster type, choose **kubeadm** (single node). With kubeadm the cluster uses the same Docker image store, so the `kubeshop/*` images you build are visible to it right away. (With *kind*, you may get `ErrImageNeverPull`. If so, switch to kubeadm.)
3. **Apply & restart**, then wait until the status shows Kubernetes running (green).

```powershell
kubectl config get-contexts          # list clusters kubectl knows about; * = current
kubectl config use-context docker-desktop
kubectl cluster-info                 # where is the API server?
kubectl get nodes -o wide            # your single node
kubectl get pods -A                  # -A = all namespaces: you'll see the control plane running as pods!
kubectl api-resources                # every kind of object this cluster understands
```

### Build the images (once, or after code changes)

```powershell
docker build -f src/Services/KubeShop.Catalog.Api/Dockerfile          -t kubeshop/catalog:0.1 .
docker build -f src/Services/KubeShop.Orders.Api/Dockerfile           -t kubeshop/orders:0.1 .
docker build -f src/Services/KubeShop.Notifications.Worker/Dockerfile -t kubeshop/notifications:0.1 .
```

### Create a namespace and make it the default

```powershell
kubectl create namespace kubeshop
kubectl config set-context --current --namespace=kubeshop   # every command below now targets kubeshop
```

✍️ **Task:** write `deploy/k8s/00-namespace.yaml` for the same namespace.
Hint: `kubectl create namespace kubeshop --dry-run=client -o yaml`. Then `kubectl apply -f` it. It says *unchanged*, because the namespace already exists.

---

## 1. Pods

A **Pod** is the smallest thing Kubernetes runs: one or more containers that share an IP address and volumes.
On its own a Pod is **mortal**: if it dies, nothing brings it back.

### Imperative

```powershell
kubectl run catalog --image=kubeshop/catalog:0.1 --image-pull-policy=Never --port=8080
kubectl get pods                     # STATUS should become Running
kubectl get pods -o wide             # + pod IP and node
kubectl describe pod catalog         # full details. Always read the "Events" section at the bottom
kubectl get pod catalog -o yaml      # what the cluster actually stored (notice all the defaults it added)
```

Talk to it. `port-forward` tunnels a local port to the pod. It blocks, so run it in a **second terminal**:

```powershell
kubectl port-forward pod/catalog 8081:8080
# first terminal:
curl.exe http://localhost:8081/products
curl.exe http://localhost:8081/info        # "Host" = the pod name
```

Go inside the container:

```powershell
kubectl exec -it catalog -- sh             # (exit with Ctrl+D)
kubectl exec catalog -- env                # look at its environment variables
```

Delete it and notice that nothing recreates it:

```powershell
kubectl delete pod catalog
kubectl get pods
```

### Declarative (the way you'll actually work)

✍️ **Task:** write `deploy/k8s/01-catalog-pod.yaml`:
- kind `Pod`, name `catalog`, namespace `kubeshop`
- labels `app: catalog`, `tier: backend`, `env: dev`
- one container `catalog`, image `kubeshop/catalog:0.1`, `imagePullPolicy: Never` (use the local image and never try Docker Hub), `containerPort: 8080` (.NET images listen on 8080)

```powershell
kubectl run catalog --image=kubeshop/catalog:0.1 --dry-run=client -o yaml   # starting point
kubectl explain pod.spec.containers.imagePullPolicy
kubectl apply -f deploy/k8s/01-catalog-pod.yaml
```

Edit something in the file (for example, add a label) and `apply` again. kubectl only sends the difference.
`kubectl diff -f <file>` shows what *would* change.

---

## 2. Labels

Labels are key/value tags. They're how Kubernetes objects find each other: Services pick pods by label, and Deployments own pods by label.

```powershell
kubectl get pods --show-labels
kubectl get pods -L app,tier,env               # show labels as columns

kubectl label pod catalog owner=me             # add
kubectl label pod catalog env=test --overwrite # change
kubectl label pod catalog owner-               # remove (trailing minus)

# Selectors
kubectl get pods -l app=catalog
kubectl get pods -l 'tier=backend,env!=prod'
kubectl get pods -l 'env in (dev,test)'
kubectl get pods -l '!owner'                   # pods WITHOUT the label
kubectl delete pods -l env=test                # bulk operations by label
```

**Annotations** look similar but are for metadata that nothing selects on (notes, tool settings):
`kubectl annotate pod catalog note="testing labels"`.

---

## 3. ConfigMaps

A ConfigMap holds non-secret configuration. A pod can use it as **environment variables** or as **files** (files are a later lesson).
.NET reads env vars with `__` as the section separator: `Catalog__BaseUrl` → `Catalog:BaseUrl`.

```powershell
# Imperative
kubectl create configmap orders-config `
  --from-literal=Catalog__BaseUrl=http://catalog `
  --from-literal=Orders__MaxQuantity=5
#   other sources: --from-file=appsettings.json   --from-env-file=.env

kubectl get configmaps
kubectl describe configmap orders-config
kubectl get configmap orders-config -o yaml
```

✍️ **Task:** write `deploy/k8s/02-orders-configmap.yaml` with the same two keys and the label `app: orders`.
Hint: add `--dry-run=client -o yaml` to the create command. Note that all values under `data:` are strings, so quote `"5"`.

---

## 4. Secrets

The same idea as a ConfigMap, but for sensitive values. ⚠️ **Values are only base64-encoded, not encrypted.** Anyone with read access to Secrets can read them. Kubernetes treats Secrets more carefully in other ways: separate RBAC permissions, they aren't printed by `describe`, and they can be encrypted at rest in etcd.

```powershell
kubectl create secret generic orders-secret --from-literal=api-key=super-secret-key

kubectl get secrets
kubectl describe secret orders-secret                        # shows sizes only, not values
kubectl get secret orders-secret -o yaml                     # shows base64 values

# Decode it to prove base64 is not protection:
$b64 = kubectl get secret orders-secret -o jsonpath='{.data.api-key}'
[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64))
```

✍️ **Task (optional):** write `deploy/k8s/03-orders-secret.yaml`. Look at the difference between `data:` (base64 values) and `stringData:` (plain text that Kubernetes encodes for you): `kubectl explain secret.stringData`.
⚠️ Add that file to `.gitignore`. Real secrets never go into git.

### Use both in a pod

✍️ **Task:** write `deploy/k8s/04-orders-pod.yaml`. It's like the catalog pod (name/labels `orders`, image `kubeshop/orders:0.1`) plus:
- `envFrom` → `configMapRef` `orders-config`: every ConfigMap key becomes an env var
- `env` → an env var named `Orders__ApiKey` whose `valueFrom.secretKeyRef` points to key `api-key` of Secret `orders-secret`

```powershell
kubectl explain pod.spec.containers.envFrom
kubectl explain pod.spec.containers.env.valueFrom.secretKeyRef
kubectl apply -f deploy/k8s/04-orders-pod.yaml
kubectl exec orders -- printenv | Select-String "Catalog|Orders"
kubectl logs orders                     # "Orders API starting. Catalog: http://catalog, max quantity: 5"
```

Now try placing an order (port-forward in a second terminal):

```powershell
kubectl port-forward pod/orders 8082:8080

$headers = @{ 'X-Api-Key' = 'super-secret-key' }
Invoke-RestMethod -Method Post http://localhost:8082/orders -Headers $headers `
  -ContentType 'application/json' -Body '{"productId":1,"quantity":2}'
```

👉 You'll get **503**. Read `kubectl logs orders` and you'll find `Catalog is unreachable at http://catalog`.
The name `catalog` doesn't resolve because no **Service** with that name exists yet. Services are next lesson's main topic, so for now create the minimal ones imperatively:

```powershell
kubectl expose pod catalog --name=catalog --port=80 --target-port=8080
kubectl expose pod orders  --name=orders  --port=80 --target-port=8080
kubectl get svc catalog -o yaml           # look at spec.selector: it copied the pod's labels
kubectl get endpointslices                # the "catalog" service found your catalog pod BY ITS LABELS
```

(`expose` copies *all* the pod's labels into the selector. Later you'll write Services in YAML with just `app: catalog`.)

Retry the POST and it works. Also try `quantity: 7` (more than MaxQuantity=5 from the ConfigMap) and a wrong API key.

> ConfigMap/Secret values injected as **env vars are read only when the container starts**.
> Change the ConfigMap and the running pod won't notice until it's recreated.

---

## 5. Deployments

Bare pods don't come back after they die, and you can't easily scale or update them. A **Deployment** fixes that:

```
Deployment (desired: 2 replicas of template v1)
   └── ReplicaSet (keeps exactly N pods matching its selector)
         ├── Pod
         └── Pod
```

First remove the bare pods and the services that `expose` created (their selectors include `env: dev`, which the deployment pods won't have):

```powershell
kubectl delete pod catalog orders
kubectl delete svc catalog orders
kubectl create deployment catalog --image=kubeshop/catalog:0.1 --replicas=2 --dry-run=client -o yaml
```

✍️ **Task:** write `deploy/k8s/05-deployments.yaml` (several objects in one file, separated by `---`):
1. Deployment `catalog`: 2 replicas, `selector.matchLabels: app: catalog`. The pod `template` has labels `app: catalog`, `tier: backend` and the same container as your catalog pod.
2. Deployment `orders`: 2 replicas, same pattern, with the container from your orders pod (envFrom + secretKeyRef).
3. ConfigMap `notifications-config` with `Orders__BaseUrl: "http://orders"` and `Notifications__PollSeconds: "5"`.
4. Deployment `notifications`: 1 replica, labels `app: notifications`, `tier: worker`, image `kubeshop/notifications:0.1`, envFrom that ConfigMap, no ports (it's a worker).

The rule that trips everyone up: **the template labels must match the selector**. Try breaking it on purpose and read the error.

```powershell
kubectl apply -f deploy/k8s/05-deployments.yaml
kubectl expose deployment catalog --port=80 --target-port=8080   # selector = app=catalog this time
kubectl expose deployment orders  --port=80 --target-port=8080

kubectl get deployments,replicasets,pods     # see the 3 layers; pod names = <deploy>-<rs hash>-<random>
kubectl get pods -L app,pod-template-hash
```

### Self-healing

```powershell
kubectl get pods -w                           # -w = watch (second terminal)
kubectl delete pod <one-of-the-orders-pods>   # a replacement appears immediately
```

### Scaling

```powershell
kubectl scale deployment catalog --replicas=4
kubectl scale deployment catalog --replicas=2
```

### Labels ↔ Deployments: the "quarantine" trick

The ReplicaSet counts pods **by label**. Take a pod out of its selector:

```powershell
kubectl label pod <an-orders-pod> app=orders-debug --overwrite
kubectl get pods -L app
```

The ReplicaSet now sees only 1 `app=orders` pod and creates a new one. The relabelled pod keeps running but has no owner, and the Service stops sending it traffic.
People use this in production to pull a misbehaving pod aside for debugging. Delete it when you're done.

### Rolling updates and rollback

```powershell
kubectl rollout history deployment/orders
kubectl set image deployment/orders orders=kubeshop/orders:9.9   # a tag that doesn't exist (container name must match yours)
kubectl rollout status deployment/orders                          # hangs. Ctrl+C
kubectl get pods                                                  # new pod: ErrImageNeverPull; old pods still serve traffic
kubectl rollout undo deployment/orders
```

When you rebuild an image with a new tag (e.g. `0.2`), roll it out with `kubectl set image`, or by editing the YAML and running `apply`.

### Watch replicas break in-memory state

```powershell
kubectl logs deploy/notifications -f
```

Place a few orders through the Service from **inside** the cluster with a throw-away pod:

```powershell
kubectl run tmp --rm -it --image=busybox:1.37 --restart=Never -- sh
# inside:
wget -qO- http://orders/info ; echo          # repeat a few times: Host changes = load balancing
wget -qO- --header 'X-Api-Key: super-secret-key' --header 'Content-Type: application/json' \
     --post-data '{"productId":2,"quantity":1}' http://orders/orders
wget -qO- http://orders/orders               # repeat: the list differs depending on which pod answers!
```

Each Orders pod has its own memory. That's why stateful data belongs in a database (a later lesson: PostgreSQL).

---

## 6. Logs and debugging

Containers should log to **stdout/stderr**. Kubernetes stores that output per container, and `kubectl logs` reads it.

### Reading logs

```powershell
kubectl logs <pod>                         # everything since the container started
kubectl logs <pod> -f                      # follow (like tail -f)
kubectl logs <pod> --tail=20               # last 20 lines
kubectl logs <pod> --since=5m              # last 5 minutes
kubectl logs <pod> --timestamps            # prefix with k8s timestamps
kubectl logs <pod> --previous              # the PREVIOUS (crashed) container instance. Essential for crash loops
kubectl logs <pod> -c <container>          # pick a container in a multi-container pod
kubectl logs deploy/orders                 # one pod of the deployment
kubectl logs -l app=orders --prefix -f     # ALL pods with the label, each line prefixed with its pod name
```

### Manipulating logs in PowerShell

```powershell
kubectl logs -l app=orders --prefix | Select-String "fail:|warn:"          # filter
kubectl logs deploy/orders | Select-String "Boom" -Context 0,6             # match + 6 lines after (stack trace)
kubectl logs deploy/orders > orders.log                                    # save to a file
(kubectl logs deploy/notifications | Select-String "Notification sent").Count   # count
```

**Structured (JSON) logs** turn logs into objects you can query. Switch the .NET console formatter with an env var.
`kubectl set env` changes the pod template, which triggers a rolling restart:

```powershell
kubectl set env deployment/orders Logging__Console__FormatterName=json
kubectl rollout status deployment/orders

# Generate some traffic (port-forward to a deployment picks one pod):
kubectl port-forward deploy/orders 8082:8080
curl.exe http://localhost:8082/debug/boom
Invoke-RestMethod -Method Post http://localhost:8082/orders -Headers @{ 'X-Api-Key' = 'wrong' } `
  -ContentType 'application/json' -Body '{"productId":1,"quantity":1}'

# Query the logs as objects:
kubectl logs deploy/orders | ConvertFrom-Json | Where-Object LogLevel -ne 'Information' |
  Select-Object LogLevel, Category, Message | Format-Table -Wrap

kubectl set env deployment/orders Logging__Console__FormatterName-    # trailing minus removes the variable
```

**Changing the log level without rebuilding:**

```powershell
kubectl set env deployment/notifications Logging__LogLevel__Default=Debug
kubectl logs deploy/notifications -f        # now you also see "Fetched N orders" debug lines
```

### The debugging toolbox

```powershell
kubectl get pods                               # STATUS + RESTARTS columns: your first clue
kubectl describe pod <pod>                     # Events at the bottom: scheduling, image pulls, probe failures, OOMKilled…
kubectl events --for pod/<pod>                 # events for one object
kubectl get events --sort-by=.lastTimestamp    # recent events in the namespace
kubectl get pod <pod> -o yaml                  # status.containerStatuses has lastState, exit code, reason
kubectl get pod <pod> -o jsonpath='{.status.containerStatuses[0].lastState}'
kubectl exec -it <pod> -- sh                   # look around inside
kubectl debug -it <pod> --image=busybox:1.37 --target=orders   # attach a debug container that has tools (wget, ps, nslookup)
kubectl run tmp --rm -it --image=busybox:1.37 --restart=Never -- sh   # test DNS/network from inside the cluster
```

### Break-it exercises 🔨

Diagnose each one using only kubectl, then fix it:

| # | Break it | What you'll see | Where the answer is |
|---|---|---|---|
| 1 | `kubectl run orders-broken --image=kubeshop/orders:0.1 --image-pull-policy=Never` (no config at all) | `Error` → `CrashLoopBackOff`, RESTARTS keeps increasing | `kubectl logs orders-broken --previous` → "Orders:ApiKey is not configured" |
| 2 | `kubectl delete secret orders-secret`, then `kubectl rollout restart deployment/orders` | New pods stuck in `CreateContainerConfigError` (old pods keep running) | `kubectl describe pod` → Events: secret "orders-secret" not found. Fix: recreate the secret |
| 3 | `kubectl set image deployment/catalog catalog=kubeshop/catalog:typo` | `ErrImageNeverPull` | `describe` Events → `kubectl rollout undo` |
| 4 | `kubectl edit configmap orders-config` → set `Catalog__BaseUrl` to `http://katalog` | **Nothing happens!** Then after `kubectl rollout restart deployment/orders`, POSTs return 503 | env vars are read only at startup; `kubectl logs -l app=orders` → "Catalog is unreachable"; nslookup from a busybox pod |
| 5 | `curl.exe http://localhost:8082/debug/boom` | HTTP 500, but the pod keeps running | the stack trace in `kubectl logs` |
| 6 | `kubectl scale deployment orders --replicas=0` | Notifications worker logs warnings | `kubectl logs deploy/notifications` → "Orders API is unavailable" |

Pod states you'll meet:

| STATUS | Meaning | First command |
|---|---|---|
| `Pending` | Can't be scheduled yet (resources, node selector) | `describe` → Events |
| `ContainerCreating` | Pulling the image / mounting volumes | `describe` |
| `ErrImagePull` / `ImagePullBackOff` / `ErrImageNeverPull` | Wrong image name/tag, registry access | `describe` |
| `CreateContainerConfigError` | Missing ConfigMap/Secret it references | `describe` |
| `CrashLoopBackOff` | The app starts and exits; k8s retries with growing back-off | `logs --previous` |
| `OOMKilled` (in lastState) | Exceeded its memory limit | `describe` / `-o yaml` |

---

## Cleanup

```powershell
kubectl delete namespace kubeshop          # deletes everything inside it
kubectl config set-context --current --namespace=default
```

## Cheat sheet

```powershell
kubectl get <kind> [-o wide|yaml|json|name] [-w] [-l sel] [-A]
kubectl describe <kind> <name>
kubectl apply -f <file|dir>      kubectl delete -f <file|dir>      kubectl diff -f <file>
kubectl explain <kind>.<field>
kubectl <create|run> ... --dry-run=client -o yaml   # generate YAML
kubectl logs / exec / port-forward / debug
kubectl rollout status|history|undo|restart deployment/<name>
kubectl scale deployment/<name> --replicas=N
kubectl set image|env deployment/<name> ...
```

## Next lessons (ideas)

Services in depth (ClusterIP/NodePort/LoadBalancer) and Ingress → probes (liveness/readiness, already in the app at `/health/*`) → resources requests/limits → ConfigMaps as files and hot reload → PostgreSQL with StatefulSet + PersistentVolumes → RabbitMQ + Redis → Helm/Kustomize.
