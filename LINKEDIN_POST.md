I come from an agriculture background. I know what a late frost does to a crop —
not from a textbook, but from watching it happen.

So when I was looking for a project that would push my .NET skills into new
territory, I built AgriPredict: a frost risk and rainfall prediction API powered
by ML.NET and 5 years of real weather data.

Here's what the stack looks like:

🌱 .NET 8 Minimal API — clean, fast, no ceremony
🤖 ML.NET FastForest classifier — trained on Open-Meteo archive data
  (59.86°N Uppsala: 1,461 labelled rows, 80/20 split, seed 42)
📊 Model metrics: Accuracy 0.85 · AUC 0.89 · F1 0.81
🐳 Docker + docker-compose — single command startup
📡 Serilog structured logging · Swagger on all endpoints · FluentValidation
🗺 Live demo: Leaflet.js map → click any location → get frost risk

The system answers the question a farmer actually asks:
"Will there be frost in the next 48 hours at my location?"

What I'd add before putting this in production:

→ Automated retraining pipeline (model drift is real — climate patterns shift)
→ OpenTelemetry + Grafana (prediction latency needs to be observable)
→ PostGIS spatial queries (farms are polygons, not points)
→ Feature store (training and inference must use identical feature transforms)
→ SHAP values (a farmer won't trust a black box — they need to see why)
→ Kafka for IoT sensor streams (real-time field data beats API polling)
→ Dedicated rainfall classifier (right now rainfall uses the frost model as a
  proxy — honest for a demo, wrong for production)

Full source, architecture diagram, and live demo:
👉 github.com/okalangkenneth/AgriPredict

#dotnet #mlnet #precisionagriculture #machinelearning #csharp #docker
