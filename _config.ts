import { Validator } from 'jsonschema';
import block from './block.json';
import item from './item.json';

const validator = new Validator()

validator.addSchema(block, '/block')
validator.addSchema(item, '/item')

export default {
  validator,
  schemas: {
    block: {
      name: 'Block',
      schema: block
    },
    item: {
      name: 'Item',
      schema: item
    }
  }
}
