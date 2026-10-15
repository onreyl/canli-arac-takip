import "server-only";
import { apiBaseUrl } from "./config";

// Docker iç adresi istemci import zincirine girmemelidir.
export const apiInternalBaseUrl = process.env.API_INTERNAL_BASE_URL?.trim().replace(/\/+$/, "") || apiBaseUrl;
