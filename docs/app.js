'use strict';

const API_BASE = 'http://localhost:5080';

// ── State ─────────────────────────────────────────────────────────────────────
let selectedLat = null;
let selectedLon = null;
let marker = null;

// ── Helpers ───────────────────────────────────────────────────────────────────
function riskColour(prob) {
  if (prob < 0.35)  return 'var(--risk-low)';
  if (prob <= 0.65) return 'var(--risk-medium)';
  return 'var(--risk-high)';
}

function riskLabel(label) {
  const map = { Low: '🟢 LOW RISK', Medium: '🟡 MEDIUM RISK', High: '🔴 HIGH RISK' };
  return map[label] ?? label;
}

function formatTime(isoString) {
  const d = new Date(isoString);
  const hh = String(d.getUTCHours()).padStart(2, '0');
  const mm = String(d.getUTCMinutes()).padStart(2, '0');
  return `${hh}:${mm} UTC`;
}

function fmtCoord(v, decimals) {
  return Number(v).toFixed(decimals ?? 4);
}

// ── DOM refs ──────────────────────────────────────────────────────────────────
const displayLat    = document.getElementById('display-lat');
const displayLon    = document.getElementById('display-lon');
const btnAnalyse    = document.getElementById('btn-analyse');
const btnUppsala    = document.getElementById('btn-uppsala');
const resultsEl     = document.getElementById('results');
const errorCard     = document.getElementById('error-card');
const frostBlock    = document.getElementById('frost-block');
const rainfallBlock = document.getElementById('rainfall-block');
const metaRow       = document.getElementById('meta-row');
const frostProb     = document.getElementById('frost-prob');
const riskBadge     = document.getElementById('risk-badge');
const rainfallGrid  = document.getElementById('rainfall-grid');
const metaModel     = document.getElementById('meta-model');
const metaTime      = document.getElementById('meta-time');

// ── Coordinate display ────────────────────────────────────────────────────────
function setCoordinates(lat, lon) {
  selectedLat = lat;
  selectedLon = lon;

  displayLat.textContent = fmtCoord(lat);
  displayLon.textContent = fmtCoord(lon);
  displayLat.classList.add('has-value');
  displayLon.classList.add('has-value');

  btnAnalyse.disabled = false;
  btnAnalyse.setAttribute('aria-disabled', 'false');
}

// ── Marker ────────────────────────────────────────────────────────────────────
function placeMarker(map, lat, lon) {
  if (marker) map.removeLayer(marker);
  marker = L.circleMarker([lat, lon], {
    radius: 8,
    color: 'var(--accent, #7ec850)',
    fillColor: 'var(--accent, #7ec850)',
    fillOpacity: 0.85,
    weight: 2,
  }).addTo(map);
}

// ── Results UI ────────────────────────────────────────────────────────────────
function showError() {
  errorCard.hidden = false;
  frostBlock.hidden = true;
  rainfallBlock.hidden = true;
  metaRow.hidden = true;
  resultsEl.classList.add('visible');
}

function hideError() {
  errorCard.hidden = true;
}

function renderFrost(data) {
  const prob = data.frostRiskProbability;
  const colour = riskColour(prob);

  frostProb.textContent = Math.round(prob * 100) + '%';
  frostProb.style.color = colour;

  riskBadge.textContent = riskLabel(data.frostRiskLabel);
  riskBadge.style.color = colour;

  frostBlock.hidden = false;
}

function renderRainfall(data) {
  const probs = data.rainfallProbabilityByDay ?? [];
  rainfallGrid.innerHTML = '';

  probs.forEach((p, i) => {
    const col = document.createElement('div');
    col.className = 'rainfall-day';

    const label = document.createElement('div');
    label.className = 'rainfall-day-label';
    label.textContent = `DAY ${i + 1}`;

    const probEl = document.createElement('div');
    probEl.className = 'rainfall-day-prob';
    probEl.textContent = Math.round(p * 100) + '%';
    probEl.style.color = riskColour(p);

    col.appendChild(label);
    col.appendChild(probEl);
    rainfallGrid.appendChild(col);
  });

  rainfallBlock.hidden = false;
}

function renderMeta(frostData) {
  metaModel.textContent = `Model v${frostData.modelVersion}`;
  metaTime.textContent  = `Generated ${formatTime(frostData.generatedAt)}`;
  metaRow.hidden = false;
}

// ── Fetch & render ────────────────────────────────────────────────────────────
async function runPrediction(lat, lon) {
  btnAnalyse.disabled = true;
  btnAnalyse.textContent = '⟳ FETCHING...';

  hideError();
  frostBlock.hidden = true;
  rainfallBlock.hidden = true;
  metaRow.hidden = true;
  resultsEl.classList.remove('visible');

  try {
    const [frostRes, rainfallRes] = await Promise.all([
      fetch(`${API_BASE}/api/v1/predict/frost?lat=${lat}&lon=${lon}`),
      fetch(`${API_BASE}/api/v1/predict/rainfall?lat=${lat}&lon=${lon}&days=3`),
    ]);

    if (!frostRes.ok || !rainfallRes.ok) {
      showError();
      return;
    }

    const [frostData, rainfallData] = await Promise.all([
      frostRes.json(),
      rainfallRes.json(),
    ]);

    renderFrost(frostData);
    renderRainfall(rainfallData);
    renderMeta(frostData);

    resultsEl.classList.add('visible');

  } catch {
    showError();
  } finally {
    btnAnalyse.disabled = false;
    btnAnalyse.setAttribute('aria-disabled', 'false');
    btnAnalyse.textContent = '▶ ANALYSE LOCATION';
  }
}

// ── Init ──────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {

  // Leaflet map centred on Uppsala
  const map = L.map('map', { zoomControl: true }).setView([59.86, 17.64], 5);

  // CartoDB Dark Matter — purpose-built dark tile layer, no CSS filter needed
  L.tileLayer(
    'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
    {
      attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors · © <a href="https://carto.com/">CARTO</a>',
      subdomains: 'abcd',
      maxZoom: 19,
    }
  ).addTo(map);

  // Map click → place marker + update coords
  map.on('click', (e) => {
    const { lat, lng } = e.latlng;
    placeMarker(map, lat, lng);
    setCoordinates(lat, lng);
  });

  // Analyse button
  btnAnalyse.addEventListener('click', () => {
    if (selectedLat !== null && selectedLon !== null) {
      runPrediction(selectedLat, selectedLon);
    }
  });

  // Uppsala demo button
  btnUppsala.addEventListener('click', () => {
    const lat = 59.86;
    const lon = 17.64;
    map.setView([lat, lon], 8, { animate: true });
    placeMarker(map, lat, lon);
    setCoordinates(lat, lon);
    runPrediction(lat, lon);
  });

});
