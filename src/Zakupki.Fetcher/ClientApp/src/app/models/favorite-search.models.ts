export interface FavoriteSearchEnqueueRequest {
  query: string;
  collectingEndLimit: string;
  expiredOnly: boolean;
  top?: number;
  limit?: number;
}
