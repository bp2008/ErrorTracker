<template>
	<div class="prop">
		<div class="propWrapper">
			<label class="propLabel" :for="inputId" v-html="spec.labelHtml" v-if="spec.inputType !== 'checkbox'">
			</label>
			<span v-if="spec.inputType === 'range'">: {{myValue}}</span>
			<div v-if="spec.inputType === 'select'">
				<select v-model="myValue" @change="onChange">
					<option v-for="v in spec.enumValues" :key="v">{{v}}</option>
				</select>
			</div>
			<div v-else-if="spec.inputType === 'array'" class="arrayEditor">
				<div class="arrayElement" v-for="(item, index) in arrayValues" :key="index">
					<select v-if="spec.allowedValues" v-model="item.value" @change="onChange">
						<option v-for="v in spec.allowedValues" :key="v">{{v}}</option>
					</select>
					<input v-else type="spec.arrayType" v-model="item.value" @change="onChange" />
					<input type="button" value="-" @click="removeArrayItem(index)" />
				</div>
				<div>
					<input type="button" value="+" @click="addArrayItem" />
				</div>
			</div>
			<template v-else>
				<input class="propInput" :id="inputId" ref="inputEle" v-model="myValue" :type="spec.inputType" :min="spec.min" :max="spec.max" @change="onChange" @dblclick="onDoubleClick" />
				<label class="checkboxLabel" :for="inputId" v-html="spec.labelHtml" v-if="spec.inputType === 'checkbox'">
				</label>
			</template>
		</div>
		<div class="help" v-if="spec.helpHtml" v-html="spec.helpHtml"></div>
	</div>
</template>

<script>

	export default {
		components: {},
		props:
		{
			initialValue: {
				required: true
			},
			spec: {
				type: Object, // An object containing the item key, default value, and other metadata.
				required: true
			}
		},
		data()
		{
			return {
				myUid: GetUid(),
				myValue: this.initialValue,
				arrayValues: null
			};
		},
		created()
		{
			if (this.spec.inputType === "array")
			{
				let arr = [];
				for (let i = 0; i < this.initialValue.length; i++)
					arr.push({ value: this.initialValue[i] }); // array values must be wrapped in an object to be compatible with v-model nested in v-for
				this.arrayValues = arr;
			}
		},
		mounted()
		{
		},
		computed:
		{
			inputId()
			{
				return "input_" + this.myUid;
			},
			myValueTypeEnforced()
			{
				if (this.spec.inputType === "number" || this.spec.inputType === "range")
					return new Number(this.myValue.toString()).valueOf();
				else if (this.spec.inputType === "array")
					return this.arrayValues.map(v => v.value);
				else
					return this.myValue;
			}
		},
		methods:
		{
			onChange(e)
			{
				this.$emit("valueChanged", this.spec.key, this.myValueTypeEnforced);
			},
			onDoubleClick(e)
			{
				this.myValue = this.spec.value;
				this.onChange(e);
			},
			addArrayItem()
			{
				if (this.spec.allowedValues && this.spec.allowedValues.length > 0)
					this.arrayValues.push({
						value: this.spec.allowedValues[0]
					});
				else
					this.arrayValues.push({
						value: null
					});
				this.onChange();
			},
			removeArrayItem(index)
			{
				this.arrayValues.splice(index, 1);
				this.onChange();
			},
			focus() // Called by parent component
			{
				if (this.$refs.inputEle)
					this.$refs.inputEle.focus();
			}
		},
		beforeDestroy()
		{
		}
	};
</script>

<style>
	.prop .help code
	{
		background-color: #e9e9e9;
		padding: 0px 3px;
		border: 1px solid #d5d5d5;
		margin: 1px;
		font-family: Consolas, monospace;
	}
	.showHelp .help
	{
		display: block !important;
	}
</style>
<style scoped>
	.prop
	{
		display: flex;
		flex-direction: column;
	}

	.propWrapper
	{
		max-width: 400px;
		flex: 1;
	}

	.propLabel
	{
		display: block;
		margin-bottom: 1px;
	}

	input[type="checkbox"]
	{
		margin-left: 5px;
	}

	.checkboxLabel
	{
		user-select: none;
	}

	input[type="text"],
	input[type="number"],
	input[type="range"],
	input[type="color"]
	{
		display: block;
		width: 100%;
		box-sizing: border-box;
	}

	.help
	{
		display: none;
		flex: 1;
		padding: 0px 20px;
		border-bottom: 2px solid #6ba7b5;
		color: #000000;
		background-color: #f5faff;
		margin: 5px 10px 10px 10px;
	}

	@media (min-width: 600px)
	{
		.prop
		{
			flex-direction: row;
			align-items: center;
		}

		.help
		{
			width: 100%;
			border-left: 2px solid #6ba7b5;
			border-bottom: none;
			margin: 0px 10px 0px 20px;
		}
	}
</style>