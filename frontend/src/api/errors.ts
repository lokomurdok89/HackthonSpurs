import type { Problem } from "./types";

/** Thrown for any non-2xx response. Branch on `problem.code`, never on `title`. */
export class ApiError extends Error {
  readonly problem: Problem;
  /** Retain the structured API problem for callers to inspect.
   * @param problem - Decoded API error.
   */
  constructor(problem: Problem) {
    super(problem.code);
    this.problem = problem;
  }
}
