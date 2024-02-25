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
    query = notion_client.databases.query(
        **{
            "database_id": DATABASE_ID,
        }
    )

    return {ticket["properties"]["issue"]["url"]: ticket["id"] for ticket in query['results']}


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
    github_issues = fetch_github_issues()
    notion_tickets_url_to_id = query_notion_database_for_tickets()

    github_issues = [github_issues[0]]

    for issue in github_issues:
        if issue.pull_request:
            continue

        issue_url = issue.html_url
        if issue_url in notion_tickets_url_to_id:
            issue_status = "Done" if issue.state == "closed" else "TODO"
            print("Update " + issue.title)
            update_ticket_status(notion_tickets_url_to_id[issue_url], issue.title, issue_status)
        else:
            print("Create " + issue.title)
            create_ticket(issue)


main()
