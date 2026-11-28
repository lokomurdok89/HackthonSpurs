import { useEffect, useState } from "react";
import { api, ApiError, describeProblem } from "../api/client";
import type { DocumentStatus, Locator, ReadResult } from "../api/types";

function locationText(loc: Locator) {
  switch (loc.kind) {
    case "page": return "Page " + loc.page;
    case "cells": return loc.sheet + " · " + loc.range;
    case "csvCell": return "Row " + loc.row + ", column " + loc.column;
  }
}

export function DocumentReading({ id }: { id: string }) {
  const [status, setStatus] = useState<DocumentStatus>();
  const [result, setResult] = useState<ReadResult>();
  const [notice, setNotice] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    let timer: ReturnType<typeof setTimeout>;
    let delay = 2000;
    async function check() {
      try {
        const current = await api.status(id, signal);
        if (signal.aborted) return;
        setStatus(current);
        setNotice("");
        if (current.state === "Failed") return;
        if (current.state === "Completed") {
          const reading = await api.result(id, signal);
          if (!signal.aborted) setResult(reading);
          return;
        }
        delay = 2000;
      } catch (err) {
        if (signal.aborted) return;
        if (err instanceof ApiError && err.problem.code === "result_not_ready") {
          setNotice(describeProblem("result_not_ready"));
        } else if (err instanceof ApiError && err.problem.status < 500) {
          setNotice(describeProblem(err.problem.code));
          return;
        } else {
          setNotice("Cannot reach the API. Retrying automatically.");
          delay = Math.min(delay * 2, 10000);
        }
      }
      timer = setTimeout(check, delay);
    }
    void check();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [id]);

  return (
    <section className="panel">
      <h2>Document receipt</h2>
      <p>Keep this link to check the same document: <a href={"#/documents/" + id}>{id}</a></p>
      <div role="status">
        {status && <p>{status.state} · Attempt {status.attempt}</p>}
        {status?.state === "Failed" && <p>{describeProblem("processing_failed")} ({status.failureCode})</p>}
        {notice && <p>{notice}</p>}
      </div>
      {result && <>
        <h3>Reading · {result.readerVersion}</h3>
        {!result.fragments.length && <p>No readable fragments were returned.</p>}
        <ul>{result.fragments.map((f, i) =>
          <li key={i}><strong>{locationText(f.locator)}</strong> ({f.method}): {f.text}</li>)}</ul>
        {result.warnings.length > 0 && <><h3>Reading warnings</h3><ul>{result.warnings.map((w, i) =>
          <li key={i}><strong>{locationText(w.locator)}</strong>: {w.message}</li>)}</ul></>}
      </>}
    </section>
  );
}
