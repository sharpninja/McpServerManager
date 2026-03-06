# Agent Instructions

## ⚠️ PRIORITY ORDER — NON-NEGOTIABLE ⚠️

**Speed is never more important than following workspace procedures.**

Before doing ANY work on ANY user request, you MUST complete these steps in order:

1. **Read `AGENTS-README-FIRST.yaml`** in the repo root for the current API key and endpoints
2. **GET `/health`** to verify the MCP server is running
3. **POST `/mcpserver/sessionlog`** with your session turn — do NOT proceed until this succeeds
4. **GET `/mcpserver/sessionlog?limit=5`** to review recent session history for context
5. **GET `/mcpserver/todo`** to check current tasks
6. **THEN** begin working on the user's request

On EVERY subsequent user message:
1. Post a new session log turn (`Add-McpSessionTurn`) before starting work.
2. Complete the user's request.
3. Update the turn with results (`Response`) and actions (`Add-McpAction`) when done.

**If you skip any of these steps, STOP and go back and do them before continuing.**
Session logging is not optional, not deferred, and not secondary to the task.
Failure to maintain the session log is a compliance violation.

## ⚠️ REFACTORING VERIFICATION — NON-NEGOTIABLE ⚠️

After ANY string rename, route change, pattern replacement, or symbol rename:

1. **GREP the entire `src/` tree** for the OLD pattern before declaring the rename complete.
2. The grep MUST return **zero matches**. If any remain, fix them before proceeding.
3. Do NOT rely on known files or memory — patterns may exist in files you didn't anticipate.

```powershell
# Example: after renaming "mcp/" to "mcpserver/" in route strings
grep -rn '"mcp/' src/ --include="*.cs"   # MUST return 0 results
```

**A rename is not complete until the verification grep confirms zero remaining instances.**
This rule exists because a prior session shipped a partial rename that broke voice endpoints
at runtime — a failure that would have been caught by a single grep.
