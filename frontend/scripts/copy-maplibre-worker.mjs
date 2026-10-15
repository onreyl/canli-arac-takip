// MapLibre 6 haritayı ayrı bir web worker dosyasında işler ve bu dosyayı kendi paketinin yanından yüklemeye çalışır.
// Next.js paketleyicisi (Turbopack) bu dosyayı çıktıya koymadığı için tarayıcı 404 alıyordu. Çözüm: dosyayı public/'e
// kopyalayıp adresini setWorkerUrl ile vermek. Kurulumda ve her dev/build öncesinde çalışır; sürüm hep paketle aynı kalır.
import { copyFileSync, mkdirSync } from "node:fs";

mkdirSync("public/maplibre", { recursive: true });
copyFileSync("node_modules/maplibre-gl/dist/maplibre-gl-worker.mjs", "public/maplibre/maplibre-gl-worker.mjs");
