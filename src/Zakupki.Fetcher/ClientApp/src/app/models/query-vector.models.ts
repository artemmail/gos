export interface UserQueryVectorDto {
  id: string;
  userId: string;
  query: string;
  vector: number[] | null;
  createdAt: string;
  updatedAt?: string | null;
  completedAt?: string | null;
}

export interface CreateUserQueryVectorRequest {
  query: string;
}
