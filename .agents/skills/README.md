# Vendored skills

These agent skills are copied into the repository so every coding session on Mubsir can use them.

| Skill | Source | Commit | Licence |
| --- | --- | --- | --- |
| `agent-reach` | https://github.com/Panniantong/Agent-Reach (`agent_reach/skill`, English version) | c62128afe64c | MIT |
| `reverse-engineer-anything` | https://github.com/morluto/rea (`.agents/skills/reverse-engineer-anything`) | 89317a39fab2 | MIT |
| `taste-skill` (skill name `design-taste-frontend`) | https://github.com/Leonxlnx/taste-skill (`skills/taste-skill`) | 12af471f627f | MIT |
| `find-skills` | https://github.com/vercel-labs/skills (`skills/find-skills`) | 9197d919034d | MIT |

Tools they need, installed on demand:

- Agent Reach CLI: `pipx install https://github.com/Panniantong/agent-reach/archive/main.zip`
- REA MCP server: `npx rea-agents setup` (run on the Windows machine that has the target app)
- taste-skill scripts: Node.js (`node <skill>/scripts/draw.mjs`, `inspect.mjs` uses the pre-installed Playwright/Chromium)
- find-skills: `npx skills find <query>` (needs npm registry access)

To update, copy the skill folder again from the source repository and change the commit above.
