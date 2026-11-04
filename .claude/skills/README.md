# Vendored skills

These Claude Code skills are copied into the repository so every session on Mubsir can use them.

| Skill | Source | Commit | Licence |
| --- | --- | --- | --- |
| `agent-reach` | https://github.com/Panniantong/Agent-Reach (`agent_reach/skill`, English version) | c62128afe64c | MIT |
| `reverse-engineer-anything` | https://github.com/morluto/rea (`.agents/skills/reverse-engineer-anything`) | 89317a39fab2 | MIT |

Tools they need, installed on demand:

- Agent Reach CLI: `pipx install https://github.com/Panniantong/agent-reach/archive/main.zip`
- REA MCP server: `npx rea-agents setup` (run on the Windows machine that has the target app)

To update, copy the skill folder again from the source repository and change the commit above.
