const value = process.env.NEXT_PUBLIC_API_BASE_URL;
if (!value?.trim()) {
  throw new Error("NEXT_PUBLIC_API_BASE_URL tanımlanmalı; frontend/.env.example dosyasını kontrol edin.");
}
export const apiBaseUrl = value.trim().replace(/\/+$/, "");
