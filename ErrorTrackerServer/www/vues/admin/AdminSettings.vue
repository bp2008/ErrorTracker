<template>
	<div class="pageRoot">
		<div v-if="(error)" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="editObj && editSpec" class="">
			<Editor :object="editObj" :spec="editSpec" @valueChanged="valueChanged" />
			<div class="buttonRow">
				<input type="button" value="Save Changes" @click="SaveChanges" /> <input type="button" value="Forget Changes / Reload Form" @click="ForgetChanges" />
			</div>
			<div class="buttonRow">
				<input type="button" value="Restart Server" @click="RestartServerClicked" />
			</div>
		</div>
	</div>
</template>

<script>
	import Editor from 'appRoot/vues/common/editor/Editor.vue';
	import { GetSettingsData, SetSettingsData, RestartServer } from 'appRoot/api/SettingsData';
	import { ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';

	export default {
		components: { Editor },
		data()
		{
			return {
				error: null,
				loading: false,
				editObj: null,
				editSpec: null
			};
		},
		computed:
		{
		},
		methods:
		{
			loadSettings()
			{
				this.loading = true;
				this.editObj = null;
				this.editSpec = null;
				GetSettingsData()
					.then(data =>
					{
						if (data.success)
						{
							this.editObj = data.settings;
							this.editSpec = data.editSpec;

							if (data.settings.loginStyle)
								appContext.loginStyle = data.settings.loginStyle;
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
			valueChanged(key, value)
			{
				this.editObj[key] = value;
			},
			SaveChanges()
			{
				this.loading = true;
				this.editObj.appPath = '/' + this.editObj.appPath.replace(/^\/+|\/+$/g, '') + '/';
				if (this.editObj.appPath === '//')
					this.editObj.appPath = '/';
				SetSettingsData(this.editObj)
					.then(data =>
					{
						if (data.success)
						{
							if (data.message)
								toaster.success(data.message);
							appContext.appPath = this.editObj.appPath;
							this.loadSettings();
						}
						else
						{
							toaster.error(data.error);
							this.loading = false;
						}
					})
					.catch(err =>
					{
						toaster.error(err.message);
						this.loading = false;
					});
			},
			ForgetChanges()
			{
				this.loadSettings();
			},
			RestartServerClicked()
			{
				ModalConfirmDialog("Are you sure you want to restart the service? Active web interface sessions will be lost.", "Confirm")
					.then(userClickedYes =>
					{
						if (userClickedYes)
						{
							this.loading = true;
							RestartServer()
								.then(data =>
								{
									setTimeout(() =>
									{
										this.loadSettings();
									}, 4000);
								})
								.catch(err =>
								{
									this.loading = false;
									toaster.error(err);
								});
						}
					});
			}
		},
		created()
		{
			this.loadSettings();
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

	.buttonRow
	{
		margin-top: 30px;
		border-top: 1px solid #AAAAAA;
		padding-top: 10px;
	}
</style>