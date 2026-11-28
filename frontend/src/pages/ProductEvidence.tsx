import { useState } from "react";
import type { ProductEvidence as Product } from "../data/types";
import { cad, numberSources, summarize } from "../data/evidence";
import { BomTable, SourcesList } from "./BomTable";
import { CostLedger } from "./CostLedger";
import { FindingsPanel } from "./FindingCard";
import { GapPlanner } from "./GapPlanner";
import { SupplierView } from "./SupplierView";
import "./ProductEvidence.css";

type TabId = "plan" | "suppliers" | "bom" | "sources";

/** Focus the element at the supplied ID after rendering and position it using the selected block alignment. */
function focusLater(id: string, block: ScrollLogicalPosition) {
  window.requestAnimationFrame(() => {
    const el = document.getElementById(id);
    if (!el) return;
    el.scrollIntoView({ block, behavior: "smooth" });
    el.focus({ preventScroll: true });
  });
}

/** Render the given product's evidence, findings, cost and supplier workspaces. */
export function ProductEvidence({ product }: { product: Product }) {
  const { total, byBucket } = summarize(product.bom);
  const highCount = product.findings.filter((f) => f.severity === "high").length;
  const bom = [...product.bom].sort((a, b) => b.costCad - a.costCad);
  const { list: sources, numberOf } = numberSources(bom, product.findings);

  const [tab, setTab] = useState<TabId>("plan");
  /** Open the evidence tab and focus the citation with the given number. */
  const goToSource = (n: number) => {
    setTab("sources");
    focusLater(`src-${n}`, "center");
  };
  /** Open the supplier section and focus its content. */
  const goToSuppliers = () => {
    setTab("suppliers");
    focusLater("tabs", "start");
  };
  const tabs: { id: TabId; label: string; count?: number }[] = [
    { id: "plan", label: "Substantiation plan" },
    { id: "suppliers", label: "Supplier declarations" },
    { id: "bom", label: "Bill of materials", count: product.bom.length },
    { id: "sources", label: "Sources", count: sources.length },
  ];

  return (
    <div className="pe">
      <header className="pe__head">
        <div className="crumbs">
          <a href="#/products">Products</a>
          <span aria-hidden="true">/</span>
          <span>{product.productId}</span>
        </div>
        <div className="pe__titlerow">
          <div>
            <p className="pe__kind">Product</p>
            <h1>
              <span className="pe__id">{product.productId}</span> {product.description}
            </h1>
          </div>
          <div className="pe__actions">
            <button type="button" className="btn" onClick={goToSuppliers}>
              Draft supplier requests
            </button>
            <button type="button" className="btn btn--primary">
              Prepare certification draft
            </button>
          </div>
        </div>
      </header>

      <section className="panel recform" aria-label="Product details">
        <dl className="recform__grid">
          <div>
            <dt>Customer</dt>
            <dd>{product.customer}</dd>
          </div>
          <div>
            <dt>Customer part</dt>
            <dd className="mono">{product.customerPart}</dd>
          </div>
          <div>
            <dt>Drawing</dt>
            <dd className="mono">{product.drawing}</dd>
          </div>
          <div>
            <dt>BOM lines</dt>
            <dd>{product.bom.length}</dd>
          </div>
          <div>
            <dt>Standard material cost</dt>
            <dd>{cad(total)} / unit</dd>
          </div>
          <div>
            <dt>Open findings</dt>
            <dd>
              {product.findings.length} ({highCount} high)
            </dd>
          </div>
        </dl>
        <p className="pe__caveat">Not an RVC result or a customs determination.</p>
      </section>

      <div className="pe__top">
        <CostLedger bom={product.bom} />

        <FindingsPanel findings={product.findings} numberOf={numberOf} onOpenSource={goToSource} />
      </div>

      <div className="panel panel--flush rectabs">
        <div className="tabs" role="tablist" aria-label="Product records" id="tabs" tabIndex={-1}>
          {tabs.map((t) => (
            <button
              key={t.id}
              type="button"
              role="tab"
              id={`tab-${t.id}`}
              aria-selected={tab === t.id}
              aria-controls={`panel-${t.id}`}
              className="tab"
              onClick={() => setTab(t.id)}
            >
              {t.label}
              {t.count !== undefined ? <span className="tab__count">{t.count}</span> : null}
            </button>
          ))}
        </div>
        <div className="tabpanel" role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`}>
          {tab === "plan" ? (
            <GapPlanner bom={product.bom} total={total} proven={byBucket.evidenced_cusma} />
          ) : null}
          {tab === "suppliers" ? (
            <SupplierView bom={product.bom} productId={product.productId} />
          ) : null}
          {tab === "bom" ? (
            <BomTable bom={bom} total={total} numberOf={numberOf} onOpenSource={goToSource} />
          ) : null}
          {tab === "sources" ? (
            <SourcesList sources={sources} />
          ) : null}
        </div>
      </div>
    </div>
  );
}
