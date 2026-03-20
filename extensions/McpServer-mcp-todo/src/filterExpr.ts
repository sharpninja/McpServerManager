/**
 * Boolean filter expression for text filters: terms, quoted phrases, !, &&, ||, and parentheses.
 * When no operators are present, space-separated terms are treated as an AND chain.
 */

export type FilterExpr =
  | { kind: 'term'; value: string }
  | { kind: 'not'; operand: FilterExpr }
  | { kind: 'and'; left: FilterExpr; right: FilterExpr }
  | { kind: 'or'; left: FilterExpr; right: FilterExpr };

type TokenType = 'term' | '&&' | '||' | '!' | '(' | ')';

interface Token {
  type: TokenType;
  value?: string;
}

function tokenize(input: string): Token[] {
  const tokens: Token[] = [];
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
      tokens.push({ type: input[index] as '(' | ')' });
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
    while (
      index < input.length &&
      !/\s/.test(input[index] ?? '') &&
      input.slice(index, index + 2) !== '&&' &&
      input.slice(index, index + 2) !== '||' &&
      input[index] !== '!' &&
      input[index] !== '(' &&
      input[index] !== ')'
    ) {
      index += 1;
    }

    tokens.push({ type: 'term', value: input.slice(start, index) });
  }

  return tokens;
}

function hasExplicitOperators(tokens: Token[]): boolean {
  return tokens.some((token) => token.type !== 'term');
}

function parsePrimary(tokens: Token[], pos: { i: number }): FilterExpr | null {
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

function parseNot(tokens: Token[], pos: { i: number }): FilterExpr | null {
  if (pos.i < tokens.length && tokens[pos.i]?.type === '!') {
    pos.i += 1;
    const operand = parseNot(tokens, pos);
    return operand === null ? null : { kind: 'not', operand };
  }

  return parsePrimary(tokens, pos);
}

function parseAnd(tokens: Token[], pos: { i: number }): FilterExpr | null {
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

function parseOr(tokens: Token[], pos: { i: number }): FilterExpr | null {
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

export function parseFilterText(input: string): FilterExpr | null {
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
      .filter((token): token is Token & { type: 'term'; value: string } => token.type === 'term')
      .map((token) => token.value);

    if (terms.length === 0) {
      return null;
    }

    return terms
      .slice(1)
      .reduce<FilterExpr>(
        (expr, term) => ({ kind: 'and', left: expr, right: { kind: 'term', value: term } }),
        { kind: 'term', value: terms[0] }
      );
  }

  return parseOr(tokens, { i: 0 });
}

export function evaluate(expr: FilterExpr | null, searchable: string): boolean {
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
