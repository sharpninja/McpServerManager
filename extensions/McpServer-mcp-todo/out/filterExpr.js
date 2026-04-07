"use strict";
/**
 * Boolean filter expression for text filters: terms, quoted phrases, !, &&, ||, and parentheses.
 * When no operators are present, space-separated terms are treated as an AND chain.
 */
Object.defineProperty(exports, "__esModule", { value: true });
exports.parseFilterText = parseFilterText;
exports.evaluate = evaluate;
function tokenize(input) {
    const tokens = [];
    let index = 0;
    while (index < input.length) {
        if (/\s/.test(input[index] ?? '')) {
            index += 1;
            continue;
        }
        if (input.slice(index, index + 2) === '&&') {
            tokens.push({ type: '&&' });
            index += 2;
            continue;
        }
        if (input.slice(index, index + 2) === '||') {
            tokens.push({ type: '||' });
            index += 2;
            continue;
        }
        if (input[index] === '!') {
            tokens.push({ type: '!' });
            index += 1;
            continue;
        }
        if (input[index] === '(' || input[index] === ')') {
            tokens.push({ type: input[index] });
            index += 1;
            continue;
        }
        if (input[index] === '"') {
            index += 1;
            const start = index;
            while (index < input.length && input[index] !== '"') {
                index += 1;
            }
            tokens.push({ type: 'term', value: input.slice(start, index) });
            if (index < input.length && input[index] === '"') {
                index += 1;
            }
            continue;
        }
        const start = index;
        while (index < input.length &&
            !/\s/.test(input[index] ?? '') &&
            input.slice(index, index + 2) !== '&&' &&
            input.slice(index, index + 2) !== '||' &&
            input[index] !== '!' &&
            input[index] !== '(' &&
            input[index] !== ')') {
            index += 1;
        }
        tokens.push({ type: 'term', value: input.slice(start, index) });
    }
    return tokens;
}
function hasExplicitOperators(tokens) {
    return tokens.some((token) => token.type !== 'term');
}
function parsePrimary(tokens, pos) {
    if (pos.i >= tokens.length) {
        return null;
    }
    const token = tokens[pos.i];
    if (token.type === '(') {
        pos.i += 1;
        const inner = parseOr(tokens, pos);
        if (pos.i < tokens.length && tokens[pos.i]?.type === ')') {
            pos.i += 1;
        }
        return inner;
    }
    if (token.type === 'term') {
        pos.i += 1;
        return { kind: 'term', value: token.value ?? '' };
    }
    pos.i += 1;
    return null;
}
function parseNot(tokens, pos) {
    if (pos.i < tokens.length && tokens[pos.i]?.type === '!') {
        pos.i += 1;
        const operand = parseNot(tokens, pos);
        return operand === null ? null : { kind: 'not', operand };
    }
    return parsePrimary(tokens, pos);
}
function parseAnd(tokens, pos) {
    let left = parseNot(tokens, pos);
    if (left === null) {
        return null;
    }
    while (pos.i < tokens.length && tokens[pos.i]?.type === '&&') {
        pos.i += 1;
        const right = parseNot(tokens, pos);
        if (right === null) {
            return left;
        }
        left = { kind: 'and', left, right };
    }
    return left;
}
function parseOr(tokens, pos) {
    let left = parseAnd(tokens, pos);
    if (left === null) {
        return null;
    }
    while (pos.i < tokens.length && tokens[pos.i]?.type === '||') {
        pos.i += 1;
        const right = parseAnd(tokens, pos);
        if (right === null) {
            return left;
        }
        left = { kind: 'or', left, right };
    }
    return left;
}
function parseFilterText(input) {
    const trimmed = input.trim();
    if (!trimmed) {
        return null;
    }
    const tokens = tokenize(trimmed);
    if (tokens.length === 0) {
        return null;
    }
    if (!hasExplicitOperators(tokens)) {
        const terms = tokens
            .filter((token) => token.type === 'term')
            .map((token) => token.value);
        if (terms.length === 0) {
            return null;
        }
        return terms
            .slice(1)
            .reduce((expr, term) => ({ kind: 'and', left: expr, right: { kind: 'term', value: term } }), { kind: 'term', value: terms[0] });
    }
    return parseOr(tokens, { i: 0 });
}
function evaluate(expr, searchable) {
    if (expr === null) {
        return true;
    }
    const haystack = searchable.toLowerCase();
    switch (expr.kind) {
        case 'term':
            return haystack.includes(expr.value.toLowerCase());
        case 'not':
            return !evaluate(expr.operand, searchable);
        case 'and':
            return evaluate(expr.left, searchable) && evaluate(expr.right, searchable);
        case 'or':
            return evaluate(expr.left, searchable) || evaluate(expr.right, searchable);
    }
}
//# sourceMappingURL=filterExpr.js.map