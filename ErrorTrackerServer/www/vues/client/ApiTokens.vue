<template>
	<div class="pageRoot">
		<div v-if="error" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="!mcpEnabled">
			<div class="heading">API Tokens</div>
			<div class="bodySection">
				API tokens let AI agents use {{systemName}} through its MCP (Model Context Protocol) endpoint. This feature is disabled. An administrator can enable it in Admin &gt; Settings.
			</div>
		</div>
		<div v-else>
			<div class="heading">API Tokens for AI Agents</div>
			<div class="bodySection">
				<p>
					A token lets an AI agent (Claude Code, Codex, VS Code Copilot, Cursor, Gemini CLI, or any other MCP client) act as you through the MCP endpoint
					<code>{{mcpUrl}}</code>. It can never do more than your account can, and only what its scopes allow.
				</p>
				<p class="warning">
					Event content is sent to the AI provider. Some values are redacted by the server's policy, but treat everything in your events as visible to the agent.
				</p>
			</div>

			<div v-if="createdToken" class="createdToken">
				<div class="heading">New Token: {{createdTokenName}}</div>
				<div class="bodySection">
					<p><b>Copy this token now. It will not be shown again.</b></p>
					<div class="tokenRow">
						<input type="text" readonly :value="createdToken" class="tokenBox" @focus="$event.target.select()" />
						<input type="button" value="Copy" @click="copy(createdToken)" />
					</div>
					<p>Ready-to-paste client configuration:</p>
					<div v-for="s in snippets" :key="s.name" class="snippet">
						<div class="snippetHeader">
							<span class="snippetName">{{s.name}}</span>
							<span class="snippetWhere">{{s.where}}</span>
							<input type="button" value="Copy" @click="copy(s.text)" />
						</div>
						<pre>{{s.text}}</pre>
					</div>
					<input type="button" value="Done" @click="createdToken = null" />
				</div>
			</div>

			<div class="heading">Create a Token</div>
			<div class="bodySection createForm">
				<div class="formRow">
					<label for="tokenName">Name</label>
					<input id="tokenName" type="text" v-model="newName" maxlength="100" placeholder="e.g. Claude Code on my laptop" class="nameBox" />
				</div>
				<div class="formRow">
					<span class="label">Scopes</span>
					<div class="checkList">
						<label v-for="s in scopes" :key="s" class="checkItem">
							<input type="checkbox" :value="s" v-model="newScopes" />
							<b>{{s}}</b> <span class="scopeDescription">{{scopeDescriptions[s]}}</span>
						</label>
					</div>
				</div>
				<div class="formRow">
					<span class="label">Projects</span>
					<div class="checkList">
						<label class="checkItem"><input type="checkbox" v-model="allProjects" /> All projects I can access</label>
						<template v-if="!allProjects">
							<label v-for="p in projects" :key="p" class="checkItem">
								<input type="checkbox" :value="p" v-model="newProjects" /> {{p}}
							</label>
						</template>
					</div>
				</div>
				<div class="formRow">
					<label for="tokenExpiry">Expires</label>
					<select id="tokenExpiry" v-model="newExpiry">
						<option value="">Never</option>
						<option value="7">In 7 days</option>
						<option value="30">In 30 days</option>
						<option value="90">In 90 days</option>
						<option value="365">In 1 year</option>
					</select>
				</div>
				<div class="formRow">
					<input type="button" value="Create Token" :disabled="creating" @click="createToken" />
				</div>
			</div>

			<div class="heading">Your Tokens</div>
			<div class="bodySection">
				<div v-if="tokens.length === 0">You have no tokens.</div>
				<table v-else class="tokenTable">
					<thead>
						<tr><th>Name</th><th>Token</th><th>Scopes</th><th>Projects</th><th>Created</th><th>Last Used</th><th>Expires</th><th>Status</th><th></th></tr>
					</thead>
					<tbody>
						<tr v-for="t in tokens" :key="t.apiTokenId" :class="{ inactive: !t.active }">
							<td>{{t.name}}</td>
							<td><code>{{t.prefix}}…</code></td>
							<td>{{t.scopes.join(", ")}}</td>
							<td>{{t.projects ? t.projects.join(", ") : "All"}}</td>
							<td>{{dateStr(t.created)}}</td>
							<td>{{t.lastUsed ? dateStr(t.lastUsed) + " from " + t.lastUsedIP : "Never"}}</td>
							<td>{{t.expires ? dateStr(t.expires) : "Never"}}</td>
							<td>{{statusStr(t)}}</td>
							<td><input v-if="!t.revoked" type="button" value="Revoke" @click="revoke(t)" /></td>
						</tr>
					</tbody>
				</table>
			</div>
		</div>
	</div>
</template>

<script>
	import { GetMyTokens, CreateToken, RevokeMyToken } from 'appRoot/api/ApiTokenData';
	import { GetDateStr, CopyToClipboard } from 'appRoot/scripts/Util';
	import { ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';

	export default {
		data()
		{
			return {
				error: null,
				loading: false,
				creating: false,
				systemName: appContext.systemName,
				mcpEnabled: false,
				mcpUrl: "",
				scopes: [],
				projects: [],
				tokens: [],
				newName: "",
				newScopes: ["events:read"],
				allProjects: true,
				newProjects: [],
				newExpiry: "",
				createdToken: null,
				createdTokenName: "",
				scopeDescriptions: {
					"events:read": "Search and read events, folders and statistics; read and preview filters; mark events read or unread for you only.",
					"events:write": "Move events and change their color, which every user sees (needs write access to the project).",
					"filters:write": "Create, change and delete filters (needs write access to the project).",
					"filters:run": "Run filters against existing events, which can move, recolor or delete them (needs write access).",
					"sensitive:read": "See values that are normally redacted (administrators only)."
				}
			};
		},
		created()
		{
			this.load();
		},
		computed:
		{
			snippets()
			{
				let url = this.mcpUrl;
				let token = this.createdToken;
				return [
					{
						name: "Claude Code", where: "Run in a terminal",
						text: 'claude mcp add --transport http errortracker ' + url + ' --header "Authorization: Bearer ' + token + '"'
					},
					{
						name: "VS Code (Copilot)", where: ".vscode/mcp.json (prompts for the token)",
						text: JSON.stringify({
							servers: { errortracker: { type: "http", url: url, headers: { Authorization: "Bearer ${input:et-token}" } } },
							inputs: [{ type: "promptString", id: "et-token", description: "ErrorTracker token", password: true }]
						}, null, 2)
					},
					{
						name: "Cursor", where: "~/.cursor/mcp.json",
						text: JSON.stringify({ mcpServers: { errortracker: { url: url, headers: { Authorization: "Bearer " + token } } } }, null, 2)
					},
					{
						name: "OpenAI Codex CLI", where: "~/.codex/config.toml, then set the ERRORTRACKER_TOKEN environment variable to the token",
						text: '[mcp_servers.errortracker]\nurl = "' + url + '"\nbearer_token_env_var = "ERRORTRACKER_TOKEN"'
					},
					{
						name: "Gemini CLI", where: "~/.gemini/settings.json",
						text: JSON.stringify({ mcpServers: { errortracker: { httpUrl: url, headers: { Authorization: "Bearer " + token } } } }, null, 2)
					},
					{
						name: "Clients that only support stdio", where: "Command for the client to run",
						text: 'npx mcp-remote ' + url + ' --header "Authorization: Bearer ' + token + '"'
					}
				];
			}
		},
		methods:
		{
			load()
			{
				this.loading = true;
				this.error = null;
				GetMyTokens()
					.then(data =>
					{
						if (data.success)
						{
							this.mcpEnabled = data.mcpEnabled;
							this.mcpUrl = data.mcpUrl;
							this.scopes = data.scopes;
							this.projects = data.projects;
							this.tokens = data.tokens;
						}
						else
							this.error = data.error;
					})
					.catch(err =>
					{
						this.error = err.message;
					})
					.finally(() =>
					{
						this.loading = false;
					});
			},
			createToken()
			{
				if (!this.newName.trim())
				{
					toaster.error("Please give the token a name.");
					return;
				}
				if (this.newScopes.length === 0)
				{
					toaster.error("Please choose at least one scope.");
					return;
				}
				if (!this.allProjects && this.newProjects.length === 0)
				{
					toaster.error("Please choose at least one project, or allow all projects.");
					return;
				}
				let expires = null;
				if (this.newExpiry)
					expires = Date.now() + parseInt(this.newExpiry) * 86400000;
				this.creating = true;
				CreateToken(this.newName.trim(), this.newScopes, this.allProjects ? null : this.newProjects, expires)
					.then(data =>
					{
						if (data.success)
						{
							this.createdToken = data.token;
							this.createdTokenName = data.info.name;
							this.mcpUrl = data.mcpUrl;
							this.tokens.unshift(data.info);
							this.newName = "";
						}
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(err);
					})
					.finally(() =>
					{
						this.creating = false;
					});
			},
			revoke(t)
			{
				ModalConfirmDialog("Revoke token \"" + t.name + "\"? Agents using it will stop working immediately.", "Confirm")
					.then(result =>
					{
						if (!result)
							return;
						RevokeMyToken(t.apiTokenId)
							.then(data =>
							{
								if (data.success)
								{
									toaster.success("Token revoked");
									this.load();
								}
								else
									toaster.error(data.error);
							})
							.catch(err =>
							{
								toaster.error(err);
							});
					});
			},
			copy(text)
			{
				CopyToClipboard(text);
				toaster.success("Copied to clipboard");
			},
			dateStr(ms)
			{
				return GetDateStr(new Date(ms));
			},
			statusStr(t)
			{
				if (t.revoked)
					return "Revoked";
				if (t.expires && t.expires <= Date.now())
					return "Expired";
				if (!t.active)
					return "Inactive";
				return "Active";
			}
		}
	}
</script>

<style scoped>
	.pageRoot
	{
		margin: 8px;
	}

	.loading
	{
		margin-top: 80px;
		text-align: center;
	}

	.error
	{
		color: #FF0000;
		font-weight: bold;
	}

	.warning
	{
		color: #885500;
	}

	.heading
	{
		font-size: 20px;
		border-bottom: 1px solid #000000;
		margin-bottom: 10px;
		margin-top: 30px;
		max-width: 800px;
	}

		.heading:first-child
		{
			margin-top: 0px;
		}

	.bodySection
	{
		margin: 0px 20px;
		max-width: 1100px;
	}

	.formRow
	{
		display: flex;
		align-items: flex-start;
		margin-bottom: 12px;
	}

		.formRow label:first-child,
		.formRow .label
		{
			width: 90px;
			flex: 0 0 auto;
			font-weight: bold;
		}

	.nameBox
	{
		width: 320px;
		max-width: 100%;
	}

	.checkList
	{
		display: flex;
		flex-direction: column;
	}

	.checkItem
	{
		margin-bottom: 4px;
		cursor: pointer;
	}

	.scopeDescription
	{
		color: #555555;
	}

	.createdToken
	{
		background-color: #F2FFF2;
		border: 1px solid #82bf85;
		padding: 10px;
		margin-top: 20px;
	}

	.tokenRow
	{
		display: flex;
		gap: 8px;
		margin-bottom: 12px;
	}

	.tokenBox
	{
		flex: 1 1 auto;
		max-width: 600px;
		font-family: Consolas, monospace;
	}

	.snippet
	{
		margin-bottom: 12px;
	}

	.snippetHeader
	{
		display: flex;
		align-items: center;
		gap: 10px;
	}

	.snippetName
	{
		font-weight: bold;
	}

	.snippetWhere
	{
		color: #555555;
		flex: 1 1 auto;
	}

	pre
	{
		background-color: #FFFFFF;
		border: 1px solid #CCCCCC;
		padding: 6px;
		margin: 4px 0px;
		white-space: pre-wrap;
		word-break: break-all;
	}

	.tokenTable
	{
		border-collapse: collapse;
	}

		.tokenTable th,
		.tokenTable td
		{
			border: 1px solid #CCCCCC;
			padding: 4px 8px;
			text-align: left;
			vertical-align: top;
		}

		.tokenTable th
		{
			background-color: #E8E8E8;
		}

		.tokenTable tr.inactive td
		{
			color: #888888;
		}
</style>
