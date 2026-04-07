"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || function (mod) {
    if (mod && mod.__esModule) return mod;
    var result = {};
    if (mod != null) for (var k in mod) if (k !== "default" && Object.prototype.hasOwnProperty.call(mod, k)) __createBinding(result, mod, k);
    __setModuleDefault(result, mod);
    return result;
};
Object.defineProperty(exports, "__esModule", { value: true });
const node_test_1 = require("node:test");
const assert = __importStar(require("node:assert/strict"));
const filterExpr_1 = require("../filterExpr");
(0, node_test_1.describe)('filterExpr', () => {
    (0, node_test_1.it)('treats space-separated terms as AND', () => {
        const expr = (0, filterExpr_1.parseFilterText)('alpha beta');
        assert.equal((0, filterExpr_1.evaluate)(expr, 'alpha beta rollout'), true);
        assert.equal((0, filterExpr_1.evaluate)(expr, 'alpha rollout only'), false);
    });
    (0, node_test_1.it)('supports quoted phrases and boolean operators', () => {
        const expr = (0, filterExpr_1.parseFilterText)('"alpha beta" && !gamma');
        assert.equal((0, filterExpr_1.evaluate)(expr, 'alpha beta rollout'), true);
        assert.equal((0, filterExpr_1.evaluate)(expr, 'alpha beta gamma rollout'), false);
    });
    (0, node_test_1.it)('respects parentheses and OR precedence', () => {
        const expr = (0, filterExpr_1.parseFilterText)('(alpha || beta) && release');
        assert.equal((0, filterExpr_1.evaluate)(expr, 'beta release'), true);
        assert.equal((0, filterExpr_1.evaluate)(expr, 'gamma release'), false);
    });
});
//# sourceMappingURL=filterExpr.test.js.map