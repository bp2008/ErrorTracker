<template>
	<div class="loginRecordsRoot">
		<div v-if="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="rows">
			<vue-good-table :columns="columns" :rows="rows" @on-row-click="rowClick" />
		</div>
		<div v-else>
			No data.
		</div>
	</div>
</template>

<script>
	import { GetLoginRecordsForSelf, GetLoginRecordsForUser, GetLoginRecordsGlobal } from 'appRoot/api/LoginRecordData';
	import { GetDateStr } from 'appRoot/scripts/Util';
	import { GeolocateIPDialog } from 'appRoot/scripts/ModalDialog';

	export default {
		components: {},
		props:
		{
			userName: {
				type: String,
				default: ""
			},
			allUsers: {
				type: Boolean,
				default: false
			}
		},
		data()
		{
			return {
				error: null,
				loading: false,
				columns: [
					{ label: "User Name", field: "UserName" },
					{ label: "IP Address", field: "IPAddress" },
					{ label: "Session ID", field: "SessionID" },
					{ label: "Date", field: "Date" }
				],
				rows: null
			};
		},
		created()
		{
			this.loadRecords();
		},
		methods:
		{
			loadRecords()
			{
				if (this.allUsers)
				{
					let startDate = new Date();
					startDate.setDate(startDate.getDate() - 30);
					let endDate = new Date();
					endDate.setDate(endDate.getDate() + 30);
					this.handleRecordsPromise(GetLoginRecordsGlobal(startDate.getTime(), endDate.getTime()));
				}
				else if (this.userName)
				{
					this.handleRecordsPromise(GetLoginRecordsForUser(this.userName));
				}
				else
				{
					this.handleRecordsPromise(GetLoginRecordsForSelf());
				}
			},
			handleRecordsPromise(promise)
			{
				this.loading = true;
				this.rows = null;
				this.error = null;
				promise
					.then(data =>
					{
						this.loading = false;
						if (data.success)
						{
							for (let i = 0; i < data.records.length; i++)
							{
								data.records[i].Date = GetDateStr(new Date(data.records[i].Date));
							}
							this.rows = data.records;
						}
						else
							this.error = data.error;
					})
					.catch(err =>
					{
						this.loading = false;
						this.error = err.message;
					});
			},
			rowClick(params)
			{
				GeolocateIPDialog(params.row.IPAddress);
			}
		}
	};
</script>

<style scoped>
	.loading
	{
		margin: 20px;
		text-align: center;
	}
</style>