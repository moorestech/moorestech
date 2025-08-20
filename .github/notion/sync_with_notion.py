import os
from notion_client import Client as NotionClient
from github import Github

GH_TOKEN = os.environ.get('GH_TOKEN')
NOTION_TOKEN = os.environ.get('NOTION_TOKEN')

GITHUB_REPOSITORY = "moorestech/moorestech"
DATABASE_ID = "10091c58b3084d33a5d66d64e78ca102"


github_client = Github(GH_TOKEN)
notion_client = NotionClient(auth=NOTION_TOKEN)


def fetch_github_issues():
    repo = github_client.get_repo(GITHUB_REPOSITORY)
    all_issues = repo.get_issues(state='all')
    return all_issues


def query_notion_database_for_tickets():
    issue_urls = {}
    start_cursor = None

    while True:
        query = notion_client.databases.query(
            **{
                "database_id": DATABASE_ID,
                "start_cursor": start_cursor,  # Use the start_cursor if it's not the first request
                "filter": {
                    "or": [
                        {
                            "property": 'issue',
                            "rich_text": {
                                "contains": "http"
                            }
                        },
                    ],
                },
            }
        )
        # Process the current page of results
        for ticket in query['results']:
            issue_link = ticket["properties"]["issue"]["url"]
            ticket_id = ticket["id"]
            ticket_title = ticket["properties"]["名前"]["title"][0]["text"]["content"]
            ticket_status = ticket["properties"]["ステータス"]["status"]["name"]
            issue_urls[issue_link] = {"id": ticket_id, "title": ticket_title, "status": ticket_status}

        # Check if there are more pages of data
        if not query['has_more']:
            break  # Exit the loop if there are no more pages
        start_cursor = query['next_cursor']  # Set the start_cursor for the next page of results

    return issue_urls


def create_ticket(issue):
    issue_url = issue.html_url
    issue_status = "Done" if issue.state == "closed" else "TODO"

    notion_client.pages.create(
        **{
            "parent": {
                "type": "database_id",
                "database_id": DATABASE_ID
            },
            "properties": {
                "名前": {
                    "title": [
                        {
                            "text": {
                                "content": issue.title
                            }
                        }
                    ]
                },
                "issue": {
                    "url": issue_url
                },
                "ステータス": {
                    "status": {
                        "name": issue_status
                    }
                }
            }
        }
    )


def update_ticket_status(page_id, title, issue_status):
    notion_client.pages.update(
        **{
            "page_id": page_id,
            "properties": {
                "名前": {
                    "title": [
                        {
                            "text": {
                                "content": title
                            }
                        }
                    ]
                },
                "ステータス": {
                    "status": {
                        "name": issue_status
                    }
                }
            }
        }
    )


def main():
    print("Fetching github issues")
    github_issues = fetch_github_issues()
    print("Fetched " + str(github_issues.totalCount) + " issues")

    print()

    print("Querying notion database for tickets")
    notion_tickets_url_to_id = query_notion_database_for_tickets()
    print("Found " + str(len(notion_tickets_url_to_id)) + " tickets")

    for issue in github_issues:
        if issue.pull_request:
            continue

        issue_url = issue.html_url
        if issue_url in notion_tickets_url_to_id:
            issue_status = "Done" if issue.state == "closed" else "Backlog"

            # タイトルとステータスが変わった場合のみ更新
            if notion_tickets_url_to_id[issue_url]["title"] == issue.title and notion_tickets_url_to_id[issue_url]["status"] == issue_status:
                print("Skip " + issue.title)
                continue

            print("Update " + issue.title)
            page_id = notion_tickets_url_to_id[issue_url]["id"]
            update_ticket_status(page_id, issue.title, issue_status)
        else:
            print("Create " + issue.title)
            create_ticket(issue)

        print("")


print("Start sync with notion")
main()
