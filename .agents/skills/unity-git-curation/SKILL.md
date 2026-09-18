---
name: unity-git-curation
description: Review, organize, stage, unstage, or commit changes in a local Unity Git repository while preserving user work and filtering generated artifacts. Use for Git status cleanup, semantic staging, commit preparation, or commit requests; do not use for GitHub PR, issue, or CI operations.
---

# Unity Git Curation

Use the local Git CLI as the source of truth. A Git or GitHub MCP is not needed for worktree, index, branch, or commit operations.

## Authorization boundaries

- Read-only inspection is allowed when relevant.
- Stage or unstage only when the user explicitly asks to change the index.
- Commit only when the user explicitly asks for a commit.
- Do not push, pull, merge, rebase, reset, rewrite history, delete branches, or change remotes unless the user explicitly requests that exact operation.
- Never discard, restore, or overwrite working-tree changes merely to make the repository clean.

## Inspect before changing the index

1. Resolve the repository root and run `git status --short`.
2. Review both unstaged and staged diffs. Inspect untracked files directly before classifying them.
3. Preserve unrelated changes and identify which files form one coherent change.
4. Treat scenes, Prefabs, ScriptableObjects, materials, animations, and `ProjectSettings` as high-risk serialized assets. Summarize semantic changes such as removed GameObjects, missing script references, or changed settings before staging them.

## Classify Unity changes

- Keep a Unity asset and its `.meta` file together when both exist. Confirm deletions do not leave an orphaned pair.
- Keep `Packages/manifest.json` and `Packages/packages-lock.json` together when they describe the same dependency change.
- Exclude rebuildable output such as `Library/`, `Temp/`, `Logs/`, `obj/`, and build output unless the repository explicitly treats it as source.
- Do not stage generated code or data without identifying its generator and confirming that generated output is intended to be versioned.
- Treat new Unity-generated `ProjectSettings` files as meaningful only when they encode an intentional, reproducible project setting. Otherwise leave them unstaged and report them.
- If Git reports a modification but the working-file hash equals the index blob, treat it as an index or filesystem-status artifact rather than meaningful content.

## Curate the index

- Use explicit pathspecs: `git add -- <paths>` and `git restore --staged -- <paths>`.
- Never use `git add .`, `git add -A`, or another repository-wide catch-all.
- Group unrelated changes separately when the user wants commit preparation. Do not mix documentation, dependency upgrades, generated output, and gameplay changes merely because they are present together.
- If the sandbox blocks `.git/index.lock`, request the narrow permission needed to update the index and retry the same scoped command.

## Verify

After changing the index, run:

```text
git status --short
git diff --cached --name-status
git diff --cached --stat
git diff --cached --check
```

Review the staged diff itself when deletions are large or Unity serialized assets changed. If `diff --check` reports whitespace produced by a Unity serializer, do not rewrite the asset only to silence the check; leave it unstaged or explain the tradeoff and ask before changing serialization.

Report what is staged, what remains unstaged, why each exclusion was made, and whether a commit or remote action was performed.
