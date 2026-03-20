"use strict";
const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const { evaluate, parseFilterText } = require("../filterExpr");
describe("filterExpr", () => {
    it("treats space-separated terms as AND", () => {
        const expr = parseFilterText("alpha beta");
        assert.equal(evaluate(expr, "alpha beta rollout"), true);
        assert.equal(evaluate(expr, "alpha rollout only"), false);
    });
    it("supports quoted phrases and boolean operators", () => {
        const expr = parseFilterText('"alpha beta" && !gamma');
        assert.equal(evaluate(expr, "alpha beta rollout"), true);
        assert.equal(evaluate(expr, "alpha beta gamma rollout"), false);
    });
    it("respects parentheses and OR precedence", () => {
        const expr = parseFilterText("(alpha || beta) && release");
        assert.equal(evaluate(expr, "beta release"), true);
        assert.equal(evaluate(expr, "gamma release"), false);
    });
});
