export interface FavoriteSearchEnqueueRequest {
  query: string;
  queryVectorId: string;
  collectingEndLimit: string;
  expiredOnly: boolean;
  similarityThresholdPercent: number;
  top?: number;
  limit?: number;
}
