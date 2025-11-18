export interface ScoreSection {
  score?: number | null;
  shortComment?: string | null;
  detailedComment?: string | null;
}

export interface TenderScores {
  profitability?: ScoreSection | null;
  attractiveness?: ScoreSection | null;
  risk?: ScoreSection | null;
}

export interface TenderAnalysisResult {
  scores?: TenderScores | null;
  decisionScore?: number | null;
  recommended?: boolean | null;
  summary?: string | null;
}
