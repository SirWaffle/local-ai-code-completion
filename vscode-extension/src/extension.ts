import * as vscode from 'vscode';
import fetch from 'node-fetch';

// Try it out in `playground.js`
const CSConfig = {
    SEARCH_PHARSE_END: ['.', ',', '{', '(', ' ', '-', '_', '+', '-', '*', '=', '/', '?', '<', '>']
};

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

			if (position.line <= 0) {
				return;
			}


			//vscode.comments.createCommentController
			const textBeforeCursor = document.getText();
			if (textBeforeCursor.trim() === "") {
				return { items: [] };
			}

			const prevLine = document.lineAt(position.line - 1).text;
			const curLine = document.lineAt(position.line ).text;

			const promptStr = prevLine + "\n" +  curLine;
			/*			
			if(!CSConfig.SEARCH_PHARSE_END.includes(textBeforeCursor[textBeforeCursor.length - 1]))
			{
				console.log("Searching criteria not met");
				return { items: [] };
			}*/

			console.log(promptStr);

			const body = {username: 'vscodeExtension', generationSettings: { 
				prompt: promptStr, return_sequences: 1
			} };

			const response = await fetch('http://localhost:5184/gpt/generate', {
				method: 'post',
				body: JSON.stringify(body),
				headers: {'Content-Type': 'application/json'}
			});
			const data = await response.json();

			console.log(data);

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
/*
			const lineBefore = document.lineAt(position.line - 1).text;
			const matches = lineBefore.match(regexp);
			if (matches) {
				const start = matches[1];
				const startInt = parseInt(start, 10);
				const end = matches[2];
				const endInt =
					end === '*' ? document.lineAt(position.line).text.length : parseInt(end, 10);
				const insertText = matches[3].replace(/\\n/g, '\n');

				return [
					{
						insertText,
						range: new vscode.Range(position.line, startInt, position.line, endInt),
						someTrackingId: someTrackingIdCounter++,
					},
				] as MyInlineCompletionItem[];
			}
*/
			
		},
	};

	vscode.languages.registerInlineCompletionItemProvider({ pattern: '**' }, provider);

}

interface MyInlineCompletionItem extends vscode.InlineCompletionItem {
	someTrackingId: number;
}
