import * as vscode from 'vscode';
import fetch from 'node-fetch';

// Try it out in `playground.js`

//****These should be settings eventually *******/

//URL of web server for calling with GPT requests
const WEBSERVER_URL = 'http://localhost:5184/gpt/generate';

//max number of previous lines to append to our GPT request
const PROMPT_LINES = 6;

const PROMPT_USERNAME = "user1";



export function activate(context: vscode.ExtensionContext) {
	const disposable = vscode.commands.registerCommand(
		'extension.inline-completion-settings',
		() => {
			vscode.window.showInformationMessage('Show settings');
		}
	);

	context.subscriptions.push(disposable);
	let someTrackingIdCounter = 0;

	const provider: vscode.InlineCompletionItemProvider = {
		provideInlineCompletionItems: async (document, position, context, token) => {
			console.log('provideInlineCompletionItems triggered');

			//build up current + past line text to send to GPT web service
			let promptStr = document.lineAt(position.line ).text;
			let pastLineCount = 0;
			while(pastLineCount < PROMPT_LINES && pastLineCount < position.line)
			{
				pastLineCount++;
				promptStr = document.lineAt(position.line - pastLineCount).text 
							+ "\n" + promptStr;
			}

			//console.log(promptStr);

			//configure request
			const body = {username: 'vscodeExt-' + PROMPT_USERNAME, 
				generationSettings: { 
					prompt: promptStr, 
					return_sequences: 1
				} 
			};

			//get text
			const response = await fetch(WEBSERVER_URL, {
				method: 'post',
				body: JSON.stringify(body),
				headers: {'Content-Type': 'application/json'}
			});
			const data = await response.json();

			console.log(data);

			//webserver returns NULL if the requedst was booted due to queue
			if(data.reponseText == null)
				return;

			// Add the generated code to the inline suggestion list
			const items = new Array<MyInlineCompletionItem>();
			for (let i=0; i < data.reponseText.length; i++) {
				const insertText = data.reponseText[i];
				items.push({
					insertText,
					range: new vscode.Range(position.translate(0, data.reponseText.length), position),
					someTrackingId: someTrackingIdCounter++,
				});
			}
			return { items };		
		},
	};

	vscode.languages.registerInlineCompletionItemProvider({ pattern: '**' }, provider);

}

interface MyInlineCompletionItem extends vscode.InlineCompletionItem {
	someTrackingId: number;
}
