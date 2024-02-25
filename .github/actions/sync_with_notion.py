const { Client, LogLevel } = require("@notionhq/client");

const notion = new Client({
  auth: process.env.NOTION_KEY,
  logLevel: LogLevel.DEBUG,
});

const database_id = process.env.NOTION_DATABASE_ID;

async function main() {
  const issueTitle = process.env.ISSUE_TITLE;
  const issueLink = process.env.ISSUE_LINK;
  const isOpen = process.env.ISSUE_STATUS === 'open';
  const status = isOpen ? "TODO" : "DONE";

  // Retrieve from database
  const { results: existingPages, } = await notion.databases.query({
    database_id,
    filter: {
      property: "issue",
      text: {
        equals: issueLink,
      }
    }
  });
  
  // If no page is found, create a new page
  if (existingPages.length === 0) {
    notion.pages.create({
      parent: { database_id },
      properties: {
        'issue': {
          type: 'url',
          url: issueLink,
        },
        'status': {
          type: 'select',
          select: { name: status },
        },
      }
    });
  } else {
    // If page found update the status
    notion.pages.update({
      page_id: existingPages[0].id,
      properties: {
        'status': {
          type: 'select',
          select: { name: status },
        }
      }
    });
  }
}

main();