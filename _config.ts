import block from './blocks.json';
import item from './items.json';
import challenge from './challenges.json';
import gearConnects from './ref/gearConnects.json';
import inputConnects from './ref/inputConnects.json';
import modelTransform from './ref/modelTransform.json';
import { RefResolver } from 'json-schema-ref-resolver'
import Ajv from 'ajv';

const refResolver = new RefResolver()
refResolver.addSchema(challenge)
refResolver.addSchema(block)
refResolver.addSchema(gearConnects)
refResolver.addSchema(inputConnects)
refResolver.addSchema(modelTransform)
refResolver.addSchema(item)

const ajv = new Ajv({ allErrors: true })
ajv.addSchema(refResolver.getDerefSchema('blocks'), '/block')
ajv.addSchema(refResolver.getDerefSchema('items'), '/item')
ajv.addSchema(refResolver.getDerefSchema('challenges'), '/challenge')

export default {
  validator: ajv,
  schemas: {
    block: {
      name: 'Block',
      schema: refResolver.getDerefSchema('blocks')
    },
    item: {
      name: 'Item',
      schema: refResolver.getDerefSchema('items')
    },
    challenge: {
      name: 'Challenge',
      schema: refResolver.getDerefSchema('challenges')
    }
  }
}
