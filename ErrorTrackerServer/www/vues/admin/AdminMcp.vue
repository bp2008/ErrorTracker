<template>
	<div class="pageRoot">
		<div class="intro">
			The MCP endpoint lets AI agents search events and manage filters using personal API tokens, which users create on their
			<router-link :to="{ name: 'apiTokens' }">API Tokens</router-link> page.
			It is currently <b>{{mcpEnabled ? "enabled" : "disabled"}}</b>; the switch and related options are on the
			<router-link :to="{ name: 'adminSettings' }">Settings</router-link> page.
		</div>

		<div class="heading">All API Tokens</div>
		<div class="bodySection">
			<div v-if="tokensError" class="error">{{tokensError}}</div>
			<div v-else-if="!tokens" class="loading"><ScaleLoader /> Loading…</div>
			<div v-else-if="tokens.length === 0">No tokens have been created.</div>
			<table v-else class="grid">
				<thead>
					<tr><th>User</th><th>Name</th><th>Token</th><th>Scopes</th><th>Projects</th><th>Created</th><th>Last Used</th><th>Expires</th><th>Status</th><th></th></tr>
				</thead>
				<tbody>
					<tr v-for="t in tokens" :key="t.apiTokenId" :class="{ inactive: !t.active }">
						<td>{{t.userName}}</td>
						<td>{{t.name}}</td>
						<td><code>{{t.prefix}}…</code></td>
						<td>{{t.scopes.join(", ")}}</td>
						<td>{{t.projects ? t.projects.join(", ") : "All"}}</td>
						<td>{{dateStr(t.created)}}</td>
						<td>{{t.lastUsed ? dateStr(t.lastUsed) + " from " + t.lastUsedIP : "Never"}}</td>
						<td>{{t.expires ? dateStr(t.expires) : "Never"}}</td>
						<td>{{statusStr(t)}}</td>
						<td class="actions">
							<template v-if="!t.revoked">
								<input type="button" value="Revoke" @click="revoke(t)" />
								<input v-if="t.scopes.indexOf('sensitive:read') === -1" type="button" value="Grant sensitive:read" @click="setSensitive(t, true)" />
								<input v-else type="button" value="Remove sensitive:read" @click="setSensitive(t, false)" />
							</template>
						</td>
					</tr>
				</tbody>
			</table>
		</div>

		<div class="heading">Redaction</div>
		<div class="bodySection">
			<div v-if="redactionError" class="error">{{redactionError}}</div>
			<div v-else-if="!rules" class="loading"><ScaleLoader /> Loading…</div>
			<div v-else>
				<p>
					Redaction applies to everything the MCP endpoint returns to tokens that lack the <code>sensitive:read</code> scope.
					Filters and searches still see the raw data. Matches of each rule are replaced with <code>[REDACTED]</code>;
					if the pattern defines a group named <code>secret</code>, only that group is replaced.
				</p>
				<p class="warning">
					Limitation: redaction cannot fully stop an agent from learning a redacted value by probing, for example by searching for
					guessed values and observing which events match. Searches and filter previews that refer to a <b>hidden tag</b> are
					rejected, but value patterns only hide text in the output. Use hidden tags for data that must not leave the server.
				</p>
				<div class="formRow">
					<label for="hiddenTagKeys">Hidden tag keys</label>
					<div>
						<textarea id="hiddenTagKeys" v-model="hiddenTagKeys" rows="4" cols="40" placeholder="One tag key per line"></textarea>
						<div class="hint">These tags are omitted entirely (case-insensitive); the output only lists their names.</div>
					</div>
				</div>
				<table class="grid rules">
					<thead>
						<tr><th>On</th><th>Name</th><th>Pattern (.NET regular expression)</th><th>Only these tags (comma-separated; empty = all text)</th><th></th></tr>
					</thead>
					<tbody>
						<tr v-for="(r, i) in rules" :key="i">
							<td><input type="checkbox" v-model="r.Enabled" /></td>
							<td><input type="text" v-model="r.Name" class="ruleName" /></td>
							<td><textarea v-model="r.Pattern" rows="2" class="pattern"></textarea></td>
							<td><input type="text" v-model="r.TagKeys" /></td>
							<td><input type="button" value="Remove" @click="rules.splice(i, 1)" /></td>
						</tr>
					</tbody>
				</table>
				<div class="buttonRow">
					<input type="button" value="Add Rule" @click="rules.push({ Name: '', Enabled: true, Pattern: '', TagKeys: '' })" />
					<input type="button" value="Restore Default Rules" @click="restoreDefaults" />
					<input type="button" value="Save Redaction Settings" @click="saveRedaction" />
				</div>
				<div class="testBox">
					<div><b>Test</b> (uses the rules above, even if not saved yet)</div>
					<div class="formRow">
						<label for="testTagKey">Tag key</label>
						<div>
							<input id="testTagKey" type="text" v-model="testTagKey" placeholder="Leave empty to test as a message" />
						</div>
					</div>
					<textarea v-model="testText" rows="5" class="testText" placeholder="Paste text, e.g. an HTTP header dump"></textarea>
					<div><input type="button" value="Test" @click="testRedaction" /></div>
					<pre v-if="testOutput !== null" class="testOutput">{{testOutput}}</pre>
				</div>
			</div>
		</div>

		<div class="heading">Audit Log</div>
		<div class="bodySection">
			<div class="auditFilters">
				<label>User <input type="text" v-model="auditUser" class="small" /></label>
				<label>Token ID <input type="text" v-model="auditTokenId" class="tiny" /></label>
				<label>Tool <input type="text" v-model="auditTool" class="small" /></label>
				<label>
					Period
					<select v-model="auditDays">
						<option value="1">Last day</option>
						<option value="7">Last 7 days</option>
						<option value="30">Last 30 days</option>
						<option value="0">All</option>
					</select>
				</label>
				<input type="button" value="Refresh" @click="loadAudit" />
			</div>
			<div v-if="auditError" class="error">{{auditError}}</div>
			<div v-else-if="!audit" class="loading"><ScaleLoader /> Loading…</div>
			<div v-else-if="audit.length === 0">No matching records.</div>
			<table v-else class="grid audit">
				<thead>
					<tr><th>Time</th><th>User</th><th>Token</th><th>IP</th><th>Tool</th><th>Project</th><th>Outcome</th><th>ms</th><th>Size</th><th>Arguments / Message</th></tr>
				</thead>
				<tbody>
					<tr v-for="r in audit" :key="r.McpAuditRecordId" :class="{ failed: r.Outcome !== 'ok' }">
						<td class="nowrap">{{dateStr(r.Date)}}</td>
						<td>{{r.UserName}}</td>
						<td>{{r.TokenName}} ({{r.ApiTokenId}})</td>
						<td>{{r.IPAddress}}</td>
						<td>{{r.ToolName}}</td>
						<td>{{r.ProjectName}}</td>
						<td class="nowrap">{{r.Outcome}}</td>
						<td>{{r.DurationMs}}</td>
						<td>{{r.ResultSize}}</td>
						<td class="args"><code>{{r.Arguments}}</code><div v-if="r.Message" class="message">{{r.Message}}</div></td>
					</tr>
				</tbody>
			</table>
		</div>
	</div>
</template>

<script>
	import { GetAllTokens, RevokeAnyToken, SetSensitiveRead, GetRedaction, SetRedaction, TestRedaction, GetAuditLog } from 'appRoot/api/McpAdminData';
	import { GetDateStr } from 'appRoot/scripts/Util';
	import { ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';

	export default {
		data()
		{
			return {
				mcpEnabled: !!appContext.mcpEnabled,
				tokens: null,
				tokensError: null,
				hiddenTagKeys: "",
				rules: null,
				defaultRules: [],
				redactionError: null,
				testTagKey: "",
				testText: "",
				testOutput: null,
				audit: null,
				auditError: null,
				auditUser: "",
				auditTokenId: "",
				auditTool: "",
				auditDays: "7"
			};
		},
		created()
		{
			this.loadTokens();
			this.loadRedaction();
			this.loadAudit();
		},
		methods:
		{
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
					return "Inactive (user renamed or deleted)";
				return "Active";
			},
			loadTokens()
			{
				this.tokens = null;
				this.tokensError = null;
				GetAllTokens()
					.then(data =>
					{
						if (data.success)
							this.tokens = data.tokens;
						else
							this.tokensError = data.error;
					})
					.catch(err =>
					{
						this.tokensError = err.message;
					});
			},
			revoke(t)
			{
				ModalConfirmDialog("Revoke token \"" + t.name + "\" of user \"" + t.userName + "\"? Agents using it will stop working immediately.", "Confirm")
					.then(result =>
					{
						if (result)
							this.handleResult(RevokeAnyToken(t.apiTokenId), "Token revoked", this.loadTokens);
					});
			},
			setSensitive(t, grant)
			{
				let msg = grant
					? "Grant \"sensitive:read\" to token \"" + t.name + "\" of user \"" + t.userName + "\"? The agent will see unredacted event data, including any secrets in it."
					: "Remove \"sensitive:read\" from token \"" + t.name + "\"?";
				ModalConfirmDialog(msg, "Confirm")
					.then(result =>
					{
						if (result)
							this.handleResult(SetSensitiveRead(t.apiTokenId, grant), grant ? "Scope granted" : "Scope removed", this.loadTokens);
					});
			},
			handleResult(promise, successMessage, then)
			{
				promise
					.then(data =>
					{
						if (data.success)
						{
							toaster.success(successMessage);
							if (then)
								then();
						}
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(err);
					});
			},
			loadRedaction()
			{
				this.rules = null;
				this.redactionError = null;
				GetRedaction()
					.then(data =>
					{
						if (data.success)
						{
							this.hiddenTagKeys = data.hiddenTagKeys;
							this.rules = data.rules;
							this.defaultRules = data.defaultRules;
						}
						else
							this.redactionError = data.error;
					})
					.catch(err =>
					{
						this.redactionError = err.message;
					});
			},
			restoreDefaults()
			{
				ModalConfirmDialog("Replace the rules above with the built-in default rules? (Nothing is saved until you click Save.)", "Confirm")
					.then(result =>
					{
						if (result)
							this.rules = JSON.parse(JSON.stringify(this.defaultRules));
					});
			},
			saveRedaction()
			{
				this.handleResult(SetRedaction(this.hiddenTagKeys, this.rules), "Redaction settings saved");
			},
			testRedaction()
			{
				TestRedaction(this.hiddenTagKeys, this.rules, this.testText, this.testTagKey)
					.then(data =>
					{
						if (data.success)
							this.testOutput = data.hidden ? "(This tag is hidden: it would be omitted entirely.)" : data.output;
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(err);
					});
			},
			loadAudit()
			{
				this.audit = null;
				this.auditError = null;
				let days = parseInt(this.auditDays);
				let start = days > 0 ? Date.now() - days * 86400000 : null;
				let tokenId = this.auditTokenId.trim() ? parseInt(this.auditTokenId) : null;
				GetAuditLog(this.auditUser.trim() || null, isNaN(tokenId) ? null : tokenId, this.auditTool.trim() || null, start, null, 1000)
					.then(data =>
					{
						if (data.success)
							this.audit = data.records;
						else
							this.auditError = data.error;
					})
					.catch(err =>
					{
						this.auditError = err.message;
					});
			}
		}
	}
</script>

<style scoped>
	.pageRoot
	{
		margin: 8px;
	}

	.intro
	{
		margin-bottom: 10px;
	}

	.loading
	{
		margin: 20px 0px;
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

	.bodySection
	{
		margin: 0px 20px;
	}

	.grid
	{
		border-collapse: collapse;
	}

		.grid th,
		.grid td
		{
			border: 1px solid #CCCCCC;
			padding: 4px 8px;
			text-align: left;
			vertical-align: top;
		}

		.grid th
		{
			background-color: #E8E8E8;
		}

		.grid tr.inactive td
		{
			color: #888888;
		}

		.grid tr.failed td
		{
			background-color: #FFF4F4;
		}

	.actions input
	{
		margin: 2px;
	}

	.formRow
	{
		display: flex;
		align-items: flex-start;
		margin: 10px 0px;
	}

		.formRow > label
		{
			width: 140px;
			flex: 0 0 auto;
			font-weight: bold;
		}

	.hint
	{
		color: #555555;
		font-size: 0.9em;
	}

	.ruleName
	{
		width: 220px;
	}

	.pattern
	{
		width: 480px;
		max-width: 100%;
		font-family: Consolas, monospace;
	}

	.buttonRow
	{
		margin: 10px 0px;
	}

		.buttonRow input
		{
			margin-right: 8px;
		}

	.testBox
	{
		border: 1px solid #CCCCCC;
		padding: 8px;
		max-width: 900px;
	}

	.testText
	{
		width: 100%;
		box-sizing: border-box;
		font-family: Consolas, monospace;
	}

	.testOutput
	{
		background-color: #FFFFFF;
		border: 1px solid #CCCCCC;
		padding: 6px;
		white-space: pre-wrap;
		word-break: break-all;
	}

	.auditFilters
	{
		display: flex;
		flex-wrap: wrap;
		gap: 12px;
		align-items: center;
		margin-bottom: 10px;
	}

	.small
	{
		width: 120px;
	}

	.tiny
	{
		width: 60px;
	}

	.nowrap
	{
		white-space: nowrap;
	}

	.args
	{
		max-width: 500px;
		word-break: break-all;
	}

	.message
	{
		color: #AA0000;
	}
</style>
