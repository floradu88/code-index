export type StartIndexRequest = { name: string; basePath: string; };
export type StartIndexResponse = { projectId: string; status: string; basePath: string; };
export type AskRequest = { projectId: string; question: string; };
export type AskResponse = { answer: string; };
export type ProjectStatus = "Pending"|"Indexing"|"Completed"|"Failed"|"Partial";
